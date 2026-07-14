import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TournamentContextService } from '../../core/tournament-context.service';
import { I18nService } from '../../core/i18n.service';
import { extractApiError } from '../../core/http-error';
import { Athlete, BracketFormat, Category, Club, Fight, RegistrationDetail, RoundRobinStanding } from '../../core/models';

/** A group of fights sharing the same bracket type and round, for rendering. */
interface RoundGroup {
  round: number;
  fights: Fight[];
}

/** A pool's fights grouped by round. */
interface PoolGroup {
  pool: number;
  rounds: RoundGroup[];
}

interface ConnectorPath {
  id: string;
  d: string;
}

/**
 * Draw view: generates and visualizes brackets per category and supports
 * swapping two athletes' positions before fights start.
 */
@Component({
  selector: 'app-draw',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './draw.component.html',
  styleUrl: './draw.component.css',
})
export class DrawComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);
  protected readonly canOperate = this.auth.canOperate;

  protected readonly categories = signal<Category[]>([]);
  protected readonly athletes = signal<Athlete[]>([]);
  protected readonly clubs = signal<Club[]>([]);
  private readonly registrations = signal<RegistrationDetail[]>([]);

  protected readonly globalFormat = signal<BracketFormat>('SingleElimination');
  protected readonly error = signal<string | null>(null);
  protected readonly generatingAll = signal(false);
  protected readonly showNextStepHint = signal(false);

  private readonly fightsByCategory = signal<Map<string, Fight[]>>(new Map());
  private readonly standingsByCategory = signal<Map<string, RoundRobinStanding[]>>(new Map());
  private readonly formatByCategory = signal<Map<string, BracketFormat>>(new Map());
  private readonly loadingByCategory = signal<Map<string, boolean>>(new Map());
  private readonly errorByCategory = signal<Map<string, string | null>>(new Map());
  private readonly swapSelectionByCategory = signal<Map<string, string[]>>(new Map());
  private readonly connectorRefreshVersion = signal(0);
  private connectorRefreshHandle: number | null = null;

  private readonly athleteMap = computed(() =>
    new Map(this.athletes().map((a) => [a.id, a])));

  private readonly clubMap = computed(() =>
    new Map(this.clubs().map((c) => [c.id, c])));

  ngOnInit(): void {
    if (this.context.tournamentId()) {
      this.api.getTournament(this.context.tournamentId()!).subscribe((tournament) => {
        this.context.refreshIfActive(tournament);
      });
      this.loadCategories();
      this.loadAthletes();
      this.loadClubs();
      this.loadRegistrations();
    }
  }

  @HostListener('window:resize')
  protected onWindowResize(): void {
    this.refreshConnectors();
  }

  protected onBracketScrolled(): void {
    this.refreshConnectors();
  }

  protected get tournamentId(): string | null {
    return this.context.tournamentId();
  }

  private loadCategories(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.api.getCategories(id).subscribe({
      next: (x) => {
        this.categories.set(x);
        const knownFormats = this.formatByCategory();
        const nextFormats = new Map<string, BracketFormat>();
        for (const category of x) {
          nextFormats.set(
            category.id,
            knownFormats.get(category.id)
              ?? category.drawFormat
              ?? this.globalFormat(),
          );
        }
        this.formatByCategory.set(nextFormats);
        for (const category of x) {
          this.loadFightsForCategory(category.id);
        }
      },
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.load'))),
    });
  }

  private loadAthletes(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.api.getAthletes(id).subscribe({ next: (x) => this.athletes.set(x) });
  }

  private loadClubs(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.api.getClubs(id).subscribe({ next: (x) => this.clubs.set(x) });
  }

  private loadRegistrations(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.api.getRegistrations(id).subscribe({ next: (x) => this.registrations.set(x) });
  }

  protected categoryFormat(categoryId: string): BracketFormat {
    return this.formatByCategory().get(categoryId) ?? this.globalFormat();
  }

  protected setCategoryFormat(categoryId: string, format: BracketFormat): void {
    const next = new Map(this.formatByCategory());
    next.set(categoryId, format);
    this.formatByCategory.set(next);
  }

  protected applyGlobalFormat(format: BracketFormat): void {
    this.globalFormat.set(format);
    const next = new Map(this.formatByCategory());
    for (const category of this.categories()) {
      if (!category.isLocked) {
        next.set(category.id, format);
      }
    }
    this.formatByCategory.set(next);
  }

  protected isCategoryLoading(categoryId: string): boolean {
    return this.loadingByCategory().get(categoryId) ?? false;
  }

  protected categoryError(categoryId: string): string | null {
    return this.errorByCategory().get(categoryId) ?? null;
  }

  protected fightsForCategory(categoryId: string): Fight[] {
    return this.fightsByCategory().get(categoryId) ?? [];
  }

  protected hasFights(categoryId: string): boolean {
    return this.fightsForCategory(categoryId).length > 0;
  }

  protected isCategoryRoundRobin(categoryId: string): boolean {
    const format = this.categoryFormat(categoryId);
    return format === 'RoundRobin' || format === 'RoundRobinWithKnockout';
  }

  protected isCategoryPureRoundRobin(categoryId: string): boolean {
    return this.categoryFormat(categoryId) === 'RoundRobin';
  }

  protected isCategoryRoundRobinWithKnockout(categoryId: string): boolean {
    return this.categoryFormat(categoryId) === 'RoundRobinWithKnockout';
  }

  protected isDoubleEliminationLimitExceeded(categoryId: string): boolean {
    return this.categoryFormat(categoryId) === 'DoubleElimination'
      && this.registrations().filter((registration) => registration.categoryId === categoryId).length > 32;
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

  protected connectorContainerId(categoryId: string, bracketType: 'Main' | 'Repechage'): string {
    return `bracket-${categoryId}-${bracketType.toLowerCase()}`;
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
    if (this.categoryFormat(categoryId) === 'DoubleElimination') {
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
    const targetSlotElement = targetElement.querySelector(targetSlot) as HTMLElement | null;
    const targetAnchorRect = targetSlotElement?.getBoundingClientRect() ?? targetRect;
    const x1 = sourceRect.right - bracketRect.left + bracket.scrollLeft;
    const y1 = sourceRect.top + sourceRect.height / 2 - bracketRect.top + bracket.scrollTop;
    const x2 = targetRect.left - bracketRect.left + bracket.scrollLeft;
    const y2 = targetAnchorRect.top + targetAnchorRect.height / 2 - bracketRect.top + bracket.scrollTop;
    const midX = x1 + Math.max(12, Math.min(46, (x2 - x1) * 0.5));

    return {
      id: `${source.id}-${target.id}-${targetSlot}`,
      d: `M ${x1} ${y1} L ${midX} ${y1} L ${midX} ${y2} L ${x2} ${y2}`,
    };
  }

  protected loadFightsForCategory(categoryId: string): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }

    this.setCategoryLoading(categoryId, true);
    this.setCategoryError(categoryId, null);

    this.api.getFights(id, categoryId).subscribe({
      next: (x) => {
        this.setCategoryFights(categoryId, x);
        this.setCategoryLoading(categoryId, false);

        const cat = this.categories().find((c) => c.id === categoryId);
        if (cat?.drawFormat) {
          this.setCategoryFormat(categoryId, cat.drawFormat);
        }

        if (cat?.drawFormat === 'RoundRobin' || cat?.drawFormat === 'RoundRobinWithKnockout') {
          this.loadStandingsForCategory(categoryId);
        } else {
          this.setCategoryStandings(categoryId, []);
        }
      },
      error: (err) => {
        this.setCategoryError(categoryId, extractApiError(err, this.i18n.translate('errors.load')));
        this.setCategoryLoading(categoryId, false);
      },
    });
  }

  protected loadStandingsForCategory(categoryId: string): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }

    this.api.getCategoryStandings(id, categoryId).subscribe({
      next: (x) => this.setCategoryStandings(categoryId, x),
    });
  }

  protected generateForCategory(category: Category): void {
    if (!this.canOperate()) {
      return;
    }

    if (category.isLocked) {
      return;
    }

    if (this.isDoubleEliminationLimitExceeded(category.id)) {
      this.setCategoryError(category.id, this.i18n.translate('draw.doubleEliminationMaxAthletes'));
      return;
    }

    const id = this.tournamentId;
    if (!id) {
      return;
    }

    if (this.hasFights(category.id) && !confirm(this.i18n.translate('draw.confirmRegenerate'))) {
      return;
    }

    this.setCategoryError(category.id, null);
    this.setCategorySwapSelection(category.id, []);
    this.setCategoryLoading(category.id, true);

    this.api.generateDraw(id, category.id, { format: this.categoryFormat(category.id) }).subscribe({
      next: (x) => {
        this.setCategoryFights(category.id, x);
        this.setCategoryLoading(category.id, false);
        this.showNextStepHint.set(true);
        this.loadCategories();
        if (this.isCategoryRoundRobin(category.id)) {
          this.loadStandingsForCategory(category.id);
        } else {
          this.setCategoryStandings(category.id, []);
        }
      },
      error: (err) => {
        this.setCategoryError(category.id, extractApiError(err, this.i18n.translate('errors.save')));
        this.setCategoryLoading(category.id, false);
      },
    });
  }

  protected generateAll(): void {
    if (!this.canOperate() || this.generatingAll()) {
      return;
    }

    const id = this.tournamentId;
    if (!id) {
      return;
    }

    const targets = this.categories().filter((category) => !category.isLocked);
    if (targets.length === 0) {
      return;
    }

    if (targets.some((category) => this.hasFights(category.id))
      && !confirm(this.i18n.translate('draw.confirmRegenerate'))) {
      return;
    }

    this.error.set(null);
    this.generatingAll.set(true);

    const run = (index: number): void => {
      if (index >= targets.length) {
        this.generatingAll.set(false);
        this.showNextStepHint.set(true);
        this.loadCategories();
        return;
      }

      const category = targets[index];
      if (this.isDoubleEliminationLimitExceeded(category.id)) {
        this.setCategoryError(category.id, this.i18n.translate('draw.doubleEliminationMaxAthletes'));
        run(index + 1);
        return;
      }
      this.setCategoryError(category.id, null);
      this.setCategorySwapSelection(category.id, []);
      this.setCategoryLoading(category.id, true);

      this.api.generateDraw(id, category.id, { format: this.categoryFormat(category.id) }).subscribe({
        next: (fights) => {
          this.setCategoryFights(category.id, fights);
          this.setCategoryLoading(category.id, false);
          if (this.isCategoryRoundRobin(category.id)) {
            this.loadStandingsForCategory(category.id);
          } else {
            this.setCategoryStandings(category.id, []);
          }
          run(index + 1);
        },
        error: (err) => {
          this.setCategoryError(category.id, extractApiError(err, this.i18n.translate('errors.save')));
          this.setCategoryLoading(category.id, false);
          run(index + 1);
        },
      });
    };

    run(0);
  }

  protected toggleSwap(categoryId: string, athleteId: string | null): void {
    if (!this.canOperate()) {
      return;
    }
    if (!athleteId) {
      return;
    }

    const current = this.swapSelectionForCategory(categoryId);
    if (current.includes(athleteId)) {
      this.setCategorySwapSelection(categoryId, current.filter((id) => id !== athleteId));
      return;
    }

    if (current.length >= 2) {
      this.setCategorySwapSelection(categoryId, [current[1], athleteId]);
      return;
    }

    this.setCategorySwapSelection(categoryId, [...current, athleteId]);
  }

  protected isSelected(categoryId: string, athleteId: string | null): boolean {
    return !!athleteId && this.swapSelectionForCategory(categoryId).includes(athleteId);
  }

  protected swapSelectionCount(categoryId: string): number {
    return this.swapSelectionForCategory(categoryId).length;
  }

  protected confirmSwap(categoryId: string): void {
    if (!this.canOperate()) {
      return;
    }

    const id = this.tournamentId;
    const selection = this.swapSelectionForCategory(categoryId);
    if (!id || selection.length !== 2) {
      return;
    }

    this.setCategoryError(categoryId, null);
    this.api
      .swapAthletes(id, categoryId, { athleteId1: selection[0], athleteId2: selection[1] })
      .subscribe({
        next: () => {
          this.setCategorySwapSelection(categoryId, []);
          this.loadFightsForCategory(categoryId);
        },
        error: (err) =>
          this.setCategoryError(categoryId, extractApiError(err, this.i18n.translate('errors.conflict'))),
      });
  }

  protected athleteName(
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
    const a = this.athleteMap().get(athleteId);
    return a ? `${a.lastName}, ${a.firstName}` : this.i18n.translate('draw.tbd');
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

  protected athleteClubName(athleteId: string | null, isBye: boolean): string | null {
    if (isBye && !athleteId) {
      return null;
    }
    if (!athleteId) {
      return null;
    }
    const athlete = this.athleteMap().get(athleteId);
    if (!athlete) {
      return null;
    }
    return this.clubMap().get(athlete.clubId)?.name ?? null;
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
    if (!this.isCategoryRoundRobin(categoryId)) {
      return false;
    }

    const fights = this.fightsForCategory(categoryId);
    return fights.some((fight) => !fight.isBye);
  }

  private groupRoundsFromFights(fights: Fight[]): RoundGroup[] {
    const rounds = [...new Set(fights.map((f) => f.round))].sort((a, b) => a - b);
    return rounds.map((round) => ({
      round,
      fights: fights
        .filter((f) => f.round === round)
        .sort((a, b) => a.fightNumber - b.fightNumber),
    }));
  }

  private setCategoryFights(categoryId: string, fights: Fight[]): void {
    const next = new Map(this.fightsByCategory());
    next.set(categoryId, fights);
    this.fightsByCategory.set(next);
    this.deferConnectorRefresh();
  }

  private setCategoryStandings(categoryId: string, standings: RoundRobinStanding[]): void {
    const next = new Map(this.standingsByCategory());
    next.set(categoryId, standings);
    this.standingsByCategory.set(next);
  }

  private setCategoryLoading(categoryId: string, loading: boolean): void {
    const next = new Map(this.loadingByCategory());
    next.set(categoryId, loading);
    this.loadingByCategory.set(next);
  }

  private setCategoryError(categoryId: string, message: string | null): void {
    const next = new Map(this.errorByCategory());
    next.set(categoryId, message);
    this.errorByCategory.set(next);
  }

  private swapSelectionForCategory(categoryId: string): string[] {
    return this.swapSelectionByCategory().get(categoryId) ?? [];
  }

  private setCategorySwapSelection(categoryId: string, selection: string[]): void {
    const next = new Map(this.swapSelectionByCategory());
    next.set(categoryId, selection);
    this.swapSelectionByCategory.set(next);
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
    const brackets = Array.from(document.querySelectorAll('.bracket[id^="bracket-"]')) as HTMLElement[];
    for (const bracket of brackets) {
      this.applyRoundVerticalAlignment(bracket);
    }
  }
}
