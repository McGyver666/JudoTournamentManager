import {
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { catchError, forkJoin, of, Subscription } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { SideThemeService } from '../../core/side-theme.service';
import { CategoryFightsUpdatedEvent, TournamentHubService } from '../../core/tournament-hub.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { Category, Fight, PublicAthlete, PublicClub, RoundRobinStanding, Tournament } from '../../core/models';

interface RoundGroup {
  round: number;
  fights: Fight[];
}

interface PoolGroup {
  pool: number;
  rounds: RoundGroup[];
}

interface ConnectorPath {
  id: string;
  d: string;
}

/**
 * Dedicated scoreboard page that lists every category's fights, brackets and
 * standings ("Wettkampflisten"). This content previously lived below the
 * "Alle Kämpfe" heading of the tournament overview and was moved here 1:1 so
 * the overview can stay focused on tatami status.
 */
@Component({
  selector: 'app-match-lists',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './match-lists.component.html',
  styleUrl: './match-lists.component.css',
})
export class MatchListsComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly sideTheme = inject(SideThemeService);
  private readonly hub = inject(TournamentHubService);
  private readonly route = inject(ActivatedRoute);

  /** True when opened via a guest share link (anonymous read-only access). */
  protected readonly guestMode = signal<boolean>(false);
  /** True when a guest link is invalid, disabled or expired (server rejected access). */
  protected readonly accessDenied = signal<boolean>(false);
  protected readonly tournamentId = signal<string | null>(null);
  protected readonly tournament = signal<Tournament | null>(null);
  protected readonly tournamentName = signal<string>('');
  protected readonly athletes = signal<Map<string, PublicAthlete>>(new Map());
  protected readonly clubs = signal<Map<string, PublicClub>>(new Map());
  protected readonly categories = signal<Map<string, Category>>(new Map());
  protected readonly categoryList = signal<Category[]>([]);
  protected readonly fightsByCategory = signal<Map<string, Fight[]>>(new Map());
  protected readonly standingsByCategory = signal<Map<string, RoundRobinStanding[]>>(new Map());
  protected readonly allMatchesLoading = signal<boolean>(false);
  protected readonly allMatchesError = signal<string | null>(null);
  protected readonly hubConnected = computed(() => this.hub.connected());

  protected readonly categoriesWithFights = computed(() => {
    const fightsByCategory = this.fightsByCategory();
    return this
      .categoryList()
      .filter((category) => (fightsByCategory.get(category.id)?.length ?? 0) > 0);
  });

  private fightSub?: Subscription;
  private reconnectSub?: Subscription;
  private categoryFightsSub?: Subscription;
  private querySub?: Subscription;
  private readonly connectorRefreshVersion = signal(0);
  private connectorRefreshHandle: number | null = null;

  ngOnInit(): void {
    this.querySub = this.route.queryParamMap.subscribe((queryParamMap) => {
      // The Display route uses ?tournamentId=…; the guest share link uses
      // ?tid=…&t=<token>. Support both so a single component serves both cases.
      const guestToken = queryParamMap.get('t') ?? undefined;
      const tid = queryParamMap.get('tournamentId') ?? queryParamMap.get('tid') ?? undefined;

      this.guestMode.set(!!guestToken);
      if (guestToken) {
        this.auth.setGuestToken(guestToken);
      }

      if (tid) {
        this.tournamentId.set(tid);
        this.loadData(tid);
        // Guests get a static snapshot; the realtime hub stays scoped to
        // authenticated Display clients (guest hub scope is handled separately).
        if (!guestToken) {
          void this.hub.connect(tid);
        }
      } else {
        this.tournamentId.set(null);
      }
    });

    this.fightSub = this.hub.fightUpdated$.subscribe(() => {
      const tid = this.tournamentId();
      if (tid) {
        this.refreshCategoriesAndMatches(tid);
      }
    });

    this.categoryFightsSub = this.hub.categoryFightsUpdated$.subscribe((evt) => {
      this.handleCategoryFightsUpdated(evt);
    });

    this.reconnectSub = this.hub.reconnected$.subscribe(() => {
      const tid = this.tournamentId();
      if (tid) {
        this.refreshCategoriesAndMatches(tid);
      }
    });
  }

  ngOnDestroy(): void {
    this.querySub?.unsubscribe();
    this.fightSub?.unsubscribe();
    this.reconnectSub?.unsubscribe();
    this.categoryFightsSub?.unsubscribe();
    if (this.connectorRefreshHandle !== null) {
      window.cancelAnimationFrame(this.connectorRefreshHandle);
      this.connectorRefreshHandle = null;
    }
    // Do not disconnect hub — TournamentContextService owns the connection lifecycle.
  }

  private loadData(tid: string): void {
    this.accessDenied.set(false);
    this.api.getPublicTournament(tid).subscribe({
      next: (t) => {
        this.tournament.set(t);
        this.tournamentName.set(t.name);
        this.sideTheme.applyTheme(document.documentElement, t);
      },
      error: () => {
        // A rejected guest link (disabled/expired/invalid token) surfaces as a
        // friendly notice instead of an empty list.
        if (this.guestMode()) {
          this.accessDenied.set(true);
        }
      },
    });
    this.api.getPublicAthletes(tid).subscribe(athletes => {
      this.athletes.set(new Map(athletes.map(a => [a.id, a])));
    });
    this.api.getPublicClubs(tid).subscribe(clubs => {
      this.clubs.set(new Map(clubs.map(c => [c.id, c])));
    });
    this.refreshCategoriesAndMatches(tid);
  }

  @HostListener('window:resize')
  protected onWindowResize(): void {
    this.refreshConnectors();
  }

  protected onBracketScrolled(): void {
    this.refreshConnectors();
  }

  private handleCategoryFightsUpdated(evt: CategoryFightsUpdatedEvent): void {
    const tid = this.tournamentId();
    if (!tid || evt.tournamentId !== tid) {
      return;
    }

    // A draw generation can change both fight lists and category draw metadata.
    this.refreshCategoriesAndMatches(tid);
  }

  private refreshCategoriesAndMatches(tid: string): void {
    this.api.getPublicCategories(tid).subscribe({
      next: categories => {
        const sortedCategories = [...categories].sort((a, b) => a.name.localeCompare(b.name));
        this.categoryList.set(sortedCategories);
        this.categories.set(new Map(sortedCategories.map(category => [category.id, category])));
        this.loadMatchesForCategories(tid, sortedCategories);
      },
      error: () => this.allMatchesError.set(this.i18n.translate('errors.load')),
    });
  }

  private loadMatchesForCategories(tid: string, categories: Category[]): void {
    this.allMatchesLoading.set(true);
    this.allMatchesError.set(null);

    if (categories.length === 0) {
      this.fightsByCategory.set(new Map());
      this.standingsByCategory.set(new Map());
      this.allMatchesLoading.set(false);
      return;
    }

    const fightRequests = categories.map((category) =>
      this.api.getPublicFights(tid, category.id).pipe(catchError(() => of([] as Fight[]))));

    forkJoin(fightRequests).subscribe({
      next: (results) => {
        const nextFights = new Map<string, Fight[]>();
        categories.forEach((category, index) => {
          nextFights.set(
            category.id,
            [...results[index]].sort((a, b) => this.compareFights(a, b)),
          );
        });
        this.fightsByCategory.set(nextFights);
        this.deferConnectorRefresh();
        this.loadStandingsForRoundRobinCategories(tid, categories);
      },
      error: () => {
        this.fightsByCategory.set(new Map());
        this.standingsByCategory.set(new Map());
        this.allMatchesError.set(this.i18n.translate('errors.load'));
        this.allMatchesLoading.set(false);
      },
    });
  }

  private loadStandingsForRoundRobinCategories(tid: string, categories: Category[]): void {
    const roundRobinCategories = categories.filter((category) => this.isCategoryRoundRobin(category));
    if (roundRobinCategories.length === 0) {
      this.standingsByCategory.set(new Map());
      this.allMatchesLoading.set(false);
      this.deferConnectorRefresh();
      return;
    }

    const standingRequests = roundRobinCategories.map((category) =>
      this.api.getPublicStandings(tid, category.id).pipe(catchError(() => of([] as RoundRobinStanding[]))));

    forkJoin(standingRequests).subscribe({
      next: (results) => {
        const nextStandings = new Map<string, RoundRobinStanding[]>();
        roundRobinCategories.forEach((category, index) => {
          nextStandings.set(category.id, results[index]);
        });
        this.standingsByCategory.set(nextStandings);
        this.allMatchesLoading.set(false);
        this.deferConnectorRefresh();
      },
      error: () => {
        this.standingsByCategory.set(new Map());
        this.allMatchesLoading.set(false);
        this.deferConnectorRefresh();
      },
    });
  }

  private compareFights(a: Fight, b: Fight): number {
    return a.round - b.round || a.fightNumber - b.fightNumber;
  }

  protected isCategoryRoundRobin(category: Category): boolean {
    return category.drawFormat === 'RoundRobin' || category.drawFormat === 'RoundRobinWithKnockout';
  }

  protected isCategoryPureRoundRobin(category: Category): boolean {
    return category.drawFormat === 'RoundRobin';
  }

  protected isCategoryRoundRobinWithKnockout(category: Category): boolean {
    return category.drawFormat === 'RoundRobinWithKnockout';
  }

  protected mainRoundsForCategory(categoryId: string): RoundGroup[] {
    return this.groupRounds(
      this.fightsForCategory(categoryId),
      'Main',
      this.shouldHideByeFightsForCategory(categoryId),
    );
  }

  protected roundRobinMainRoundsForCategory(categoryId: string): RoundGroup[] {
    return this.groupRounds(this.fightsForCategory(categoryId), 'Main');
  }

  protected repechageRoundsForCategory(categoryId: string): RoundGroup[] {
    return this.groupRounds(this.fightsForCategory(categoryId), 'Repechage');
  }

  protected poolGroupsForCategory(categoryId: string): PoolGroup[] {
    const groupFights = this
      .fightsForCategory(categoryId)
      .filter((fight) => fight.bracketType === 'GroupStage');
    const pools = [...new Set(groupFights.map((fight) => fight.poolNumber ?? 0))].sort((a, b) => a - b);
    return pools.map((pool) => ({
      pool,
      rounds: this.groupRoundsFromFights(
        groupFights.filter((fight) => (fight.poolNumber ?? 0) === pool),
      ),
    }));
  }

  protected roundRobinPoolGroupsForCategory(categoryId: string): PoolGroup[] {
    const groupFights = this
      .fightsForCategory(categoryId)
      .filter((fight) => fight.bracketType === 'GroupStage' && !fight.isBye);
    const pools = [...new Set(groupFights.map((fight) => fight.poolNumber ?? 0))].sort((a, b) => a - b);
    return pools.map((pool) => ({
      pool,
      rounds: this.groupRoundsFromFights(
        groupFights.filter((fight) => (fight.poolNumber ?? 0) === pool),
      ),
    }));
  }

  protected fightsForCategory(categoryId: string): Fight[] {
    return this.fightsByCategory().get(categoryId) ?? [];
  }

  protected standingsForCategory(categoryId: string): RoundRobinStanding[] {
    return this.standingsByCategory().get(categoryId) ?? [];
  }

  protected allStandingsForCategory(categoryId: string): RoundRobinStanding[] {
    return this.standingsForCategory(categoryId).filter((standing) => standing.poolNumber === 0);
  }

  protected standingsForPool(categoryId: string, pool: number): RoundRobinStanding[] {
    return this
      .standingsForCategory(categoryId)
      .filter((standing) => standing.poolNumber === pool);
  }

  private groupRounds(
    fights: Fight[],
    type: 'Main' | 'Repechage',
    excludeByes = false,
  ): RoundGroup[] {
    const relevant = fights.filter(
      (fight) => fight.bracketType === type && (!excludeByes || !fight.isBye),
    );
    return this.groupRoundsFromFights(relevant);
  }

  private shouldHideByeFightsForCategory(categoryId: string): boolean {
    const category = this.categories().get(categoryId);
    if (!category || !this.isCategoryRoundRobin(category)) {
      return false;
    }

    const fights = this.fightsForCategory(categoryId);
    return fights.some((fight) => !fight.isBye);
  }

  private groupRoundsFromFights(fights: Fight[]): RoundGroup[] {
    const rounds = [...new Set(fights.map((fight) => fight.round))].sort((a, b) => a - b);
    return rounds.map((round) => ({
      round,
      fights: fights
        .filter((fight) => fight.round === round)
        .sort((a, b) => a.fightNumber - b.fightNumber),
    }));
  }

  protected connectorContainerId(categoryId: string, bracketType: 'Main' | 'Repechage'): string {
    return `display-bracket-${categoryId}-${bracketType.toLowerCase()}`;
  }

  protected connectorSvgWidth(containerId: string): number {
    this.connectorRefreshVersion();
    const bracket = document.getElementById(containerId);
    return bracket?.scrollWidth ?? 0;
  }

  protected connectorSvgHeight(containerId: string): number {
    this.connectorRefreshVersion();
    const bracket = document.getElementById(containerId);
    return bracket?.scrollHeight ?? 0;
  }

  protected connectorSvgViewBox(containerId: string): string {
    const width = this.connectorSvgWidth(containerId);
    const height = this.connectorSvgHeight(containerId);
    return `0 0 ${Math.max(width, 1)} ${Math.max(height, 1)}`;
  }

  protected connectorPaths(categoryId: string, bracketType: 'Main' | 'Repechage'): ConnectorPath[] {
    this.connectorRefreshVersion();

    const hideByeFights = this.shouldHideByeFightsForCategory(categoryId);
    const fights = this
      .fightsForCategory(categoryId)
      .filter((fight) => fight.bracketType === bracketType && (!hideByeFights || !fight.isBye));
    if (fights.length === 0) {
      return [];
    }

    const bracketId = this.connectorContainerId(categoryId, bracketType);
    const bracket = document.getElementById(bracketId);
    if (!bracket) {
      return [];
    }

    const bracketRect = bracket.getBoundingClientRect();
    if (this.categories().get(categoryId)?.drawFormat === 'DoubleElimination') {
      return this.sourceConnectorPaths(fights, bracket, bracketRect);
    }

    const byRoundAndFight = new Map<string, Fight>();
    for (const fight of fights) {
      byRoundAndFight.set(this.roundFightKey(fight.round, fight.fightNumber), fight);
    }

    const paths: ConnectorPath[] = [];
    for (const fight of fights) {
      const nextFightNumber = Math.floor((fight.fightNumber + 1) / 2);
      const target = byRoundAndFight.get(this.roundFightKey(fight.round + 1, nextFightNumber));
      if (!target) {
        continue;
      }

      const sourceElement = bracket.querySelector(`.fight[data-fight-id="${fight.id}"]`) as HTMLElement | null;
      const targetElement = bracket.querySelector(`.fight[data-fight-id="${target.id}"]`) as HTMLElement | null;
      if (!sourceElement || !targetElement) {
        continue;
      }

      const sourceRect = sourceElement.getBoundingClientRect();
      const targetRect = targetElement.getBoundingClientRect();
      const sourceAnchorRect = this.sourceAnchorRect(sourceElement, sourceRect);
      const targetSlot = fight.fightNumber % 2 === 1 ? '.slot.white' : '.slot.blue';
      const targetSlotElement = targetElement.querySelector(targetSlot) as HTMLElement | null;
      const targetAnchorRect = targetSlotElement?.getBoundingClientRect() ?? targetRect;

      const x1 = sourceRect.right - bracketRect.left + bracket.scrollLeft;
      const y1 = sourceAnchorRect.top + sourceAnchorRect.height / 2 - bracketRect.top + bracket.scrollTop;
      const x2 = targetRect.left - bracketRect.left + bracket.scrollLeft;
      const y2 = targetAnchorRect.top + targetAnchorRect.height / 2 - bracketRect.top + bracket.scrollTop;

      const horizontalGap = x2 - x1;
      const midX = x1 + Math.max(12, Math.min(46, horizontalGap * 0.5));
      const d = `M ${x1} ${y1} L ${midX} ${y1} L ${midX} ${y2} L ${x2} ${y2}`;
      paths.push({
        id: `${fight.id}-${target.id}`,
        d,
      });
    }

    return paths;
  }

  private sourceConnectorPaths(
    fights: Fight[],
    bracket: HTMLElement,
    bracketRect: DOMRect,
  ): ConnectorPath[] {
    const fightsById = new Map(fights.map((fight) => [fight.id, fight]));
    const paths: ConnectorPath[] = [];

    for (const target of fights) {
      const sources = [
        { id: target.whiteSourceFightId, slot: '.slot.white' },
        { id: target.blueSourceFightId, slot: '.slot.blue' },
      ];

      for (const sourceReference of sources) {
        const source = sourceReference.id ? fightsById.get(sourceReference.id) : undefined;
        if (!source) {
          continue;
        }

        const path = this.connectorPathBetween(bracket, bracketRect, source, target, sourceReference.slot);
        if (path) {
          paths.push(path);
        }
      }
    }

    return paths;
  }

  private connectorPathBetween(
    bracket: HTMLElement,
    bracketRect: DOMRect,
    source: Fight,
    target: Fight,
    targetSlot: string,
  ): ConnectorPath | null {
    const sourceElement = bracket.querySelector(`.fight[data-fight-id="${source.id}"]`) as HTMLElement | null;
    const targetElement = bracket.querySelector(`.fight[data-fight-id="${target.id}"]`) as HTMLElement | null;
    if (!sourceElement || !targetElement) {
      return null;
    }

    const sourceRect = sourceElement.getBoundingClientRect();
    const targetRect = targetElement.getBoundingClientRect();
    const sourceAnchorRect = this.sourceAnchorRect(sourceElement, sourceRect);
    const targetSlotElement = targetElement.querySelector(targetSlot) as HTMLElement | null;
    const targetAnchorRect = targetSlotElement?.getBoundingClientRect() ?? targetRect;
    const x1 = sourceRect.right - bracketRect.left + bracket.scrollLeft;
    const y1 = sourceAnchorRect.top + sourceAnchorRect.height / 2 - bracketRect.top + bracket.scrollTop;
    const x2 = targetRect.left - bracketRect.left + bracket.scrollLeft;
    const y2 = targetAnchorRect.top + targetAnchorRect.height / 2 - bracketRect.top + bracket.scrollTop;
    const midX = x1 + Math.max(12, Math.min(46, (x2 - x1) * 0.5));

    return {
      id: `${source.id}-${target.id}-${targetSlot}`,
      d: `M ${x1} ${y1} L ${midX} ${y1} L ${midX} ${y2} L ${x2} ${y2}`,
    };
  }

  private sourceAnchorRect(sourceElement: HTMLElement, fallbackRect: DOMRect): DOMRect {
    const sourceHeaderElement = sourceElement.querySelector('.fight-no') as HTMLElement | null;
    return sourceHeaderElement?.getBoundingClientRect() ?? fallbackRect;
  }

  private roundFightKey(round: number, fightNumber: number): string {
    return `${round}:${fightNumber}`;
  }

  private applyRoundVerticalAlignment(bracket: HTMLElement): void {
    const context = this.bracketContext(bracket.id);
    const rounds = Array.from(bracket.querySelectorAll('.round')) as HTMLElement[];
    if (rounds.length <= 1) {
      return;
    }

    const baselineFights = Array.from(rounds[0].querySelectorAll('.fight')) as HTMLElement[];
    if (baselineFights.length === 0) {
      return;
    }

    const baselineHeight = this.averageFightHeight(baselineFights);
    const baselineCenterDistance = this.baselineCenterDistance(baselineFights, baselineHeight);

    for (let roundIndex = 0; roundIndex < rounds.length; roundIndex += 1) {
      const roundElement = rounds[roundIndex];
      const fights = Array.from(roundElement.querySelectorAll('.fight')) as HTMLElement[];
      if (fights.length === 0) {
        roundElement.classList.remove('progression-round');
        continue;
      }

      if (roundIndex > 0) {
        roundElement.classList.add('progression-round');
      } else {
        roundElement.classList.remove('progression-round');
      }

      for (const fight of fights) {
        fight.style.marginTop = '0px';
      }

      if (roundIndex === 0) {
        continue;
      }

      const desiredCenterDistance = baselineCenterDistance * (2 ** roundIndex);
      const desiredTopOffset = ((2 ** roundIndex) - 1) * baselineCenterDistance / 2;
      const desiredInterFightGap = Math.max(0, desiredCenterDistance - baselineHeight);

      const fallbackTopOffsets = fights.map((_, fightIndex) => fightIndex === 0 ? desiredTopOffset : desiredInterFightGap);
      const aligned = context
        ? this.alignRoundToVisibleSources(bracket, context.categoryId, fights, fallbackTopOffsets)
        : false;

      if (!aligned) {
        fights[0].style.marginTop = `${desiredTopOffset}px`;
        for (let fightIndex = 1; fightIndex < fights.length; fightIndex += 1) {
          fights[fightIndex].style.marginTop = `${desiredInterFightGap}px`;
        }
      }
    }
  }

  private alignRoundToVisibleSources(
    bracket: HTMLElement,
    categoryId: string,
    fights: HTMLElement[],
    fallbackTopOffsets: number[],
  ): boolean {
    const fightsById = new Map(this.fightsForCategory(categoryId).map((fight) => [fight.id, fight]));
    let usedVisibleSources = false;

    for (let fightIndex = 0; fightIndex < fights.length; fightIndex += 1) {
      const fightElement = fights[fightIndex];
      const fightId = fightElement.dataset['fightId'];
      const fightModel = fightId ? fightsById.get(fightId) : undefined;
      const sourceTop = fightModel
        ? this.desiredFightTopFromVisibleSources(bracket, fightElement, fightModel)
        : null;

      if (sourceTop === null) {
        fightElement.style.marginTop = `${fallbackTopOffsets[fightIndex]}px`;
        continue;
      }

      usedVisibleSources = true;
      const currentTop = fightElement.getBoundingClientRect().top;
      const nextMarginTop = Math.max(0, sourceTop - currentTop);
      fightElement.style.marginTop = `${nextMarginTop}px`;
    }

    return usedVisibleSources;
  }

  private desiredFightTopFromVisibleSources(
    bracket: HTMLElement,
    fightElement: HTMLElement,
    fight: Fight,
  ): number | null {
    const references = [
      { sourceId: fight.whiteSourceFightId, targetSlot: '.slot.white' },
      { sourceId: fight.blueSourceFightId, targetSlot: '.slot.blue' },
    ];

    const desiredTops: number[] = [];
    for (const reference of references) {
      if (!reference.sourceId) {
        continue;
      }

      const sourceElement = bracket.querySelector(`.fight[data-fight-id="${reference.sourceId}"]`) as HTMLElement | null;
      const targetSlotElement = fightElement.querySelector(reference.targetSlot) as HTMLElement | null;
      if (!sourceElement || !targetSlotElement) {
        continue;
      }

      const sourceRect = sourceElement.getBoundingClientRect();
      const slotRect = targetSlotElement.getBoundingClientRect();
      const fightRect = fightElement.getBoundingClientRect();
      const slotCenterOffset = slotRect.top + slotRect.height / 2 - fightRect.top;
      desiredTops.push(sourceRect.top + sourceRect.height / 2 - slotCenterOffset);
    }

    if (desiredTops.length === 0) {
      return null;
    }

    return desiredTops.reduce((sum, top) => sum + top, 0) / desiredTops.length;
  }

  private bracketContext(bracketId: string): { categoryId: string; bracketType: 'Main' | 'Repechage' } | null {
    const bracketPrefix = 'display-bracket-';
    const repechageSuffix = '-repechage';
    if (bracketId.startsWith(bracketPrefix) && bracketId.endsWith(repechageSuffix)) {
      return {
        categoryId: bracketId.slice(bracketPrefix.length, -repechageSuffix.length),
        bracketType: 'Repechage',
      };
    }

    const mainSuffix = '-main';
    if (bracketId.startsWith(bracketPrefix) && bracketId.endsWith(mainSuffix)) {
      return {
        categoryId: bracketId.slice(bracketPrefix.length, -mainSuffix.length),
        bracketType: 'Main',
      };
    }

    return null;
  }

  private averageFightHeight(fights: HTMLElement[]): number {
    if (fights.length === 0) {
      return 0;
    }

    const totalHeight = fights
      .map((fight) => fight.getBoundingClientRect().height)
      .reduce((sum, value) => sum + value, 0);
    return totalHeight / fights.length;
  }

  private baselineCenterDistance(fights: HTMLElement[], fallbackHeight: number): number {
    if (fights.length < 2) {
      return fallbackHeight + 16;
    }

    const firstCenter = this.elementVerticalCenter(fights[0]);
    const secondCenter = this.elementVerticalCenter(fights[1]);
    return Math.max(8, secondCenter - firstCenter);
  }

  private elementVerticalCenter(element: HTMLElement): number {
    const rect = element.getBoundingClientRect();
    return rect.top + rect.height / 2;
  }

  private deferConnectorRefresh(): void {
    window.setTimeout(() => this.refreshConnectors(), 0);
  }

  private refreshConnectors(): void {
    if (this.connectorRefreshHandle !== null) {
      window.cancelAnimationFrame(this.connectorRefreshHandle);
      this.connectorRefreshHandle = null;
    }

    this.connectorRefreshHandle = window.requestAnimationFrame(() => {
      this.connectorRefreshHandle = window.requestAnimationFrame(() => {
        this.connectorRefreshHandle = null;
        this.alignAllBracketContainers();
        this.connectorRefreshVersion.update((value) => value + 1);
      });
    });
  }

  private alignAllBracketContainers(): void {
    const brackets = Array.from(document.querySelectorAll('.bracket[id^="display-bracket-"]')) as HTMLElement[];
    for (const bracket of brackets) {
      this.applyRoundVerticalAlignment(bracket);
    }
  }

  protected drawAthleteName(
    athleteId: string | null,
    isBye: boolean,
    categoryId?: string,
    fight?: Fight,
    side?: 'white' | 'blue',
  ): string {
    if (isBye && !athleteId) {
      return this.i18n.translate('draw.bye');
    }
    if (!athleteId) {
      const source = categoryId && fight && side
        ? this.slotSource(categoryId, fight, side)
        : null;
      if (source?.isBye) {
        return this.i18n.translate('draw.bye');
      }
      if (source) {
        return this.i18n.translate(
          source.outcome === 'Winner' ? 'draw.sourceWinner' : 'draw.sourceLoser',
          { n: source.fightNumber },
        );
      }
      return this.i18n.translate('draw.tbd');
    }
    return this.athleteName(athleteId);
  }

  protected isPlaceholderSlot(
    athleteId: string | null,
    isBye: boolean,
    categoryId?: string,
    fight?: Fight,
    side?: 'white' | 'blue',
  ): boolean {
    if (athleteId || (isBye && !athleteId)) {
      return false;
    }

    const source = categoryId && fight && side
      ? this.slotSource(categoryId, fight, side)
      : null;
    return !source?.isBye;
  }

  private slotSource(
    categoryId: string,
    fight: Fight,
    side: 'white' | 'blue',
  ): { fightNumber: number; outcome: 'Winner' | 'Loser'; isBye: boolean } | null {
    const sourceId = side === 'white' ? fight.whiteSourceFightId : fight.blueSourceFightId;
    const sourceOutcome = side === 'white' ? fight.whiteSourceOutcome : fight.blueSourceOutcome;
    const fights = this.fightsForCategory(categoryId);

    if (sourceId && sourceOutcome) {
      const sourceFight = fights.find((candidate) => candidate.id === sourceId);
      return sourceFight ? this.sourceReference(sourceFight, sourceOutcome) : null;
    }

    if (fight.bracketType === 'Main' && fight.round > 1) {
      const sourceFightNumber = fight.fightNumber * 2 - (side === 'white' ? 1 : 0);
      const sourceFight = fights.find((candidate) => candidate.bracketType === 'Main'
        && candidate.round === fight.round - 1
        && candidate.fightNumber === sourceFightNumber);
      return sourceFight ? this.sourceReference(sourceFight, 'Winner') : null;
    }

    if (fight.bracketType === 'Repechage') {
      const mainFights = fights.filter((candidate) => candidate.bracketType === 'Main');
      const maxRound = Math.max(...mainFights.map((candidate) => candidate.round));
      const semifinal = mainFights.find((candidate) => candidate.round === maxRound - 1
        && candidate.fightNumber === (side === 'white' ? 1 : 2));
      return semifinal ? this.sourceReference(semifinal, 'Loser') : null;
    }

    return null;
  }

  private sourceReference(
    fight: Fight,
    outcome: 'Winner' | 'Loser',
  ): { fightNumber: number; outcome: 'Winner' | 'Loser'; isBye: boolean } {
    const athleteId = outcome === 'Winner'
      ? fight.winnerId
      : fight.winnerId === fight.whiteAthleteId ? fight.blueAthleteId : fight.whiteAthleteId;

    return {
      fightNumber: fight.fightNumber,
      outcome,
      isBye: fight.status === 'Completed' && athleteId === null,
    };
  }

  protected drawAthleteClubName(athleteId: string | null, isBye: boolean): string | null {
    if (isBye && !athleteId) {
      return null;
    }
    if (!athleteId) {
      return null;
    }

    const athlete = this.athletes().get(athleteId);
    if (!athlete) {
      return null;
    }
    return this.clubs().get(athlete.clubId)?.name ?? null;
  }

  protected athleteName(id: string | null): string {
    if (!id) return '?';
    const a = this.athletes().get(id);
    return a ? `${a.lastName}, ${a.firstName}` : id.substring(0, 8);
  }
}
