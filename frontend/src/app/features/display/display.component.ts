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
import { catchError, combineLatest, forkJoin, of, Subscription } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { I18nService } from '../../core/i18n.service';
import { SideThemeService } from '../../core/side-theme.service';
import { CategoryFightsUpdatedEvent, TournamentHubService } from '../../core/tournament-hub.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { Athlete, Category, Club, Fight, FightSide, RoundRobinStanding, Tatami, Tournament } from '../../core/models';

interface TatamiDisplay {
  tatami: Tatami;
  current: Fight | null;
  nextFights: Fight[];
}

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

@Component({
  selector: 'app-display',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './display.component.html',
  styleUrl: './display.component.css',
})
export class DisplayComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly i18n = inject(I18nService);
  protected readonly sideTheme = inject(SideThemeService);
  private readonly hub = inject(TournamentHubService);
  private readonly route = inject(ActivatedRoute);

  protected readonly tournamentId = signal<string | null>(null);
  protected readonly tatamiModeTatamiId = signal<string | null>(null);
  protected readonly tournament = signal<Tournament | null>(null);
  protected readonly tournamentName = signal<string>('');
  protected readonly displays = signal<TatamiDisplay[]>([]);
  protected readonly athletes = signal<Map<string, Athlete>>(new Map());
  protected readonly clubs = signal<Map<string, Club>>(new Map());
  protected readonly categories = signal<Map<string, Category>>(new Map());
  protected readonly categoryList = signal<Category[]>([]);
  protected readonly fightsByCategory = signal<Map<string, Fight[]>>(new Map());
  protected readonly standingsByCategory = signal<Map<string, RoundRobinStanding[]>>(new Map());
  protected readonly allMatchesLoading = signal<boolean>(false);
  protected readonly allMatchesError = signal<string | null>(null);
  protected readonly nowEpochMs = signal<number>(Date.now());
  protected readonly hubConnected = computed(() => this.hub.connected());
  protected readonly isTatamiMode = computed(() => this.tatamiModeTatamiId() !== null);
  protected readonly tatamiDisplay = computed(() => {
    const tatamiId = this.tatamiModeTatamiId();
    if (!tatamiId) {
      return null;
    }

    return this.displays().find((display) => display.tatami.id === tatamiId) ?? null;
  });
  protected readonly categoriesWithFights = computed(() => {
    const fightsByCategory = this.fightsByCategory();
    return this
      .categoryList()
      .filter((category) => (fightsByCategory.get(category.id)?.length ?? 0) > 0);
  });

  private fightSub?: Subscription;
  private categoryFightsSub?: Subscription;
  private querySub?: Subscription;
  private timerHandle: ReturnType<typeof setInterval> | null = null;
  private readonly connectorRefreshVersion = signal(0);
  private connectorRefreshHandle: number | null = null;

  ngOnInit(): void {
    this.querySub = combineLatest([this.route.paramMap, this.route.queryParamMap]).subscribe(([paramMap, queryParamMap]) => {
      const tid = queryParamMap.get('tournamentId') ?? undefined;
      this.tatamiModeTatamiId.set(paramMap.get('tatamiId'));

      if (tid) {
        this.tournamentId.set(tid);
        this.loadData(tid);
        void this.hub.connect(tid);
      } else {
        this.tournamentId.set(null);
        this.displays.set([]);
      }
    });

    this.fightSub = this.hub.fightUpdated$.subscribe(() => {
      const tid = this.tournamentId();
      if (tid) {
        this.refreshQueues(tid);
        this.refreshAllMatches(tid);
      }
    });

    this.categoryFightsSub = this.hub.categoryFightsUpdated$.subscribe((evt) => {
      this.handleCategoryFightsUpdated(evt);
    });

    this.timerHandle = setInterval(() => {
      this.nowEpochMs.set(Date.now());
    }, 1000);
  }

  ngOnDestroy(): void {
    this.querySub?.unsubscribe();
    this.fightSub?.unsubscribe();
    this.categoryFightsSub?.unsubscribe();
    if (this.timerHandle !== null) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
    }
    if (this.connectorRefreshHandle !== null) {
      window.cancelAnimationFrame(this.connectorRefreshHandle);
      this.connectorRefreshHandle = null;
    }
    // Do not disconnect hub — TournamentContextService owns the connection lifecycle.
  }

  private loadData(tid: string): void {
    this.api.getTournament(tid).subscribe(t => {
      this.tournament.set(t);
      this.tournamentName.set(t.name);
      this.sideTheme.applyTheme(document.documentElement, t);
    });
    this.api.getAthletes(tid).subscribe(athletes => {
      this.athletes.set(new Map(athletes.map(a => [a.id, a])));
    });
    this.api.getClubs(tid).subscribe(clubs => {
      this.clubs.set(new Map(clubs.map(c => [c.id, c])));
    });
    this.refreshCategoriesAndMatches(tid);
    this.refreshQueues(tid);
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
    this.refreshQueues(tid);
  }

  private refreshCategoriesAndMatches(tid: string): void {
    this.api.getCategories(tid).subscribe({
      next: categories => {
        const sortedCategories = [...categories].sort((a, b) => a.name.localeCompare(b.name));
        this.categoryList.set(sortedCategories);
        this.categories.set(new Map(sortedCategories.map(category => [category.id, category])));
        this.loadMatchesForCategories(tid, sortedCategories);
      },
      error: () => this.allMatchesError.set(this.i18n.translate('errors.load')),
    });
  }

  private refreshAllMatches(tid: string): void {
    this.refreshCategoriesAndMatches(tid);
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
      this.api.getFights(tid, category.id).pipe(catchError(() => of([] as Fight[]))));

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
      this.api.getCategoryStandings(tid, category.id).pipe(catchError(() => of([] as RoundRobinStanding[]))));

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
    return this.groupRounds(this.fightsForCategory(categoryId), 'Main');
  }

  protected roundRobinMainRoundsForCategory(categoryId: string): RoundGroup[] {
    return this.groupRounds(
      this.fightsForCategory(categoryId),
      'Main',
      this.shouldHideByeFightsForCategory(categoryId),
    );
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
    return category ? this.isCategoryRoundRobin(category) : false;
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
      const targetSlot = fight.fightNumber % 2 === 1 ? '.slot.white' : '.slot.blue';
      const targetSlotElement = targetElement.querySelector(targetSlot) as HTMLElement | null;
      const targetAnchorRect = targetSlotElement?.getBoundingClientRect() ?? targetRect;

      const x1 = sourceRect.right - bracketRect.left + bracket.scrollLeft;
      const y1 = sourceRect.top + sourceRect.height / 2 - bracketRect.top + bracket.scrollTop;
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

  private roundFightKey(round: number, fightNumber: number): string {
    return `${round}:${fightNumber}`;
  }

  private applyRoundVerticalAlignment(bracket: HTMLElement): void {
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

      fights[0].style.marginTop = `${desiredTopOffset}px`;
      for (let fightIndex = 1; fightIndex < fights.length; fightIndex += 1) {
        fights[fightIndex].style.marginTop = `${desiredInterFightGap}px`;
      }
    }
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

  protected drawAthleteName(athleteId: string | null, isBye: boolean): string {
    if (isBye && !athleteId) {
      return this.i18n.translate('draw.bye');
    }
    if (!athleteId) {
      return this.i18n.translate('draw.tbd');
    }
    return this.athleteName(athleteId);
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

  private refreshQueues(tid: string): void {
    this.api.getTatamis(tid).subscribe(tatamis => {
      const sortedTatamis = [...tatamis].sort((a, b) => {
        if (a.displayOrder !== b.displayOrder) {
          return a.displayOrder - b.displayOrder;
        }

        return a.name.localeCompare(b.name);
      });
      const selectedTatamiId = this.tatamiModeTatamiId();

      if (selectedTatamiId) {
        const selectedTatami = sortedTatamis.find((tatami) => tatami.id === selectedTatamiId);
        if (!selectedTatami) {
          this.displays.set([]);
          return;
        }

        this.api.getTatamiQueue(tid, selectedTatami.id).subscribe({
          next: q => {
            const currentId = q.current?.id;
            const nextFights = [q.next, q.onDeck, ...q.upcoming]
              .filter((fight): fight is Fight => !!fight && fight.id !== currentId)
              .slice(0, 3);
            this.displays.set([{ tatami: selectedTatami, current: q.current, nextFights }]);
          },
          error: () => {
            this.displays.set([{ tatami: selectedTatami, current: null, nextFights: [] }]);
          },
        });
        return;
      }

      const updates: TatamiDisplay[] = [];
      let pending = sortedTatamis.length;
      if (pending === 0) {
        this.displays.set([]);
        return;
      }
      sortedTatamis.forEach(tatami => {
        this.api.getTatamiQueue(tid, tatami.id).subscribe({
          next: q => {
            const currentId = q.current?.id;
            const nextFights = [q.next, q.onDeck, ...q.upcoming].filter((fight): fight is Fight => !!fight && fight.id !== currentId).slice(0, 3);
            updates.push({ tatami, current: q.current, nextFights });
            pending--;
            if (pending === 0) {
              this.displays.set([...updates]);
            }
          },
          error: () => {
            updates.push({ tatami, current: null, nextFights: [] });
            pending--;
            if (pending === 0) {
              this.displays.set([...updates]);
            }
          },
        });
      });
    });
  }

  protected hasIppon(fight: Fight, side: FightSide): boolean {
    return this.scoreCount(fight, side, 'ippon') > 0;
  }

  protected isOsaeKomiRunning(fight: Fight): boolean {
    return fight.osaeKomiSide !== null && fight.osaeKomiStartedAtUtc !== null;
  }

  protected osaeKomiSideLabel(fight: Fight): FightSide | null {
    if (!fight.osaeKomiSide) {
      return null;
    }

    return fight.osaeKomiSide === 'White' ? 'white' : 'blue';
  }

  protected osaeKomiSecondsLabel(fight: Fight): string {
    if (!fight.osaeKomiSide || !fight.osaeKomiStartedAtUtc) {
      return '--';
    }

    const side = this.osaeKomiSideLabel(fight);
    if (!side) {
      return '--';
    }

    const capSeconds = this.hasWazaAri(fight, side) ? 20 : 25;
    const elapsedSeconds = Math.ceil((Date.now() - new Date(fight.osaeKomiStartedAtUtc).getTime()) / 1000);
    const runningSeconds = Math.min(capSeconds, Math.max(0, elapsedSeconds));
    return `${runningSeconds}s`;
  }

  protected osaeKomiCapSecondsLabel(fight: Fight): string {
    const side = this.osaeKomiSideLabel(fight);
    if (!side) {
      return '--';
    }

    return `${this.hasWazaAri(fight, side) ? 20 : 25}s`;
  }

  private hasWazaAri(fight: Fight, side: FightSide): boolean {
    return side === 'white' ? fight.whiteWazaAriCount > 0 : fight.blueWazaAriCount > 0;
  }

  protected tatamiDisplayLink(tatamiId: string): string {
    const tid = this.tournamentId();
    if (!tid) {
      return '#';
    }

    return `/display/tatami/${tatamiId}?tournamentId=${encodeURIComponent(tid)}`;
  }

  protected athleteClub(id: string | null): string {
    if (!id) return '';
    const athlete = this.athletes().get(id);
    if (!athlete) return '';
    const club = this.clubs().get(athlete.clubId);
    return club?.name ?? '';
  }

  protected categoryName(id: string): string {
    return this.categories().get(id)?.name ?? id.substring(0, 8);
  }

  protected scoreCount(fight: Fight, side: FightSide, scoreType: 'ippon' | 'wazaAri' | 'yuko' | 'shido'): number {
    if (side === 'white') {
      switch (scoreType) {
        case 'ippon': return fight.whiteIpponCount;
        case 'wazaAri': return fight.whiteWazaAriCount;
        case 'yuko': return fight.whiteYukoCount;
        case 'shido': return Math.min(3, fight.whitePenalties);
      }
    }

    switch (scoreType) {
      case 'ippon': return fight.blueIpponCount;
      case 'wazaAri': return fight.blueWazaAriCount;
      case 'yuko': return fight.blueYukoCount;
      case 'shido': return Math.min(3, fight.bluePenalties);
    }
  }

  protected shidoSlots(): number[] {
    return [0, 1, 2];
  }

  protected timerForFight(fight: Fight): string {
    // Bind to the ticking signal to force refresh every second.
    const now = this.nowEpochMs();
    void now;

    if (!fight.startedAtUtc) return '--:--';

    const cat = this.categories().get(fight.categoryId);
    const matchDuration = cat?.matchDurationSeconds ?? 300;
    const goldenScoreEnabled = cat?.goldenScoreEnabled ?? false;
    const goldenScoreDuration = cat?.goldenScoreDurationSeconds ?? 180;

    const timerReference = fight.status === 'Paused' && fight.pausedAtUtc
      ? new Date(fight.pausedAtUtc).getTime()
      : Date.now();
    const elapsedSeconds = (timerReference - new Date(fight.startedAtUtc).getTime()) / 1000;

    if (goldenScoreEnabled && elapsedSeconds >= matchDuration) {
      // Golden-score phase: count down the golden-score duration.
      const gsElapsed = elapsedSeconds - matchDuration;
      const gsRemaining = Math.max(0, Math.ceil(goldenScoreDuration - gsElapsed));
      return this.formatTimer(gsRemaining);
    }

    const remainingSeconds = Math.max(0, Math.ceil(matchDuration - elapsedSeconds));
    return this.formatTimer(remainingSeconds);
  }

  private formatTimer(seconds: number): string {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  protected athleteName(id: string | null): string {
    if (!id) return '?';
    const a = this.athletes().get(id);
    return a ? `${a.lastName}, ${a.firstName}` : id.substring(0, 8);
  }

}
