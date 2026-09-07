import { Component, OnDestroy, computed, effect, inject, signal, untracked } from '@angular/core';
import { catchError, forkJoin, map, of, Subscription, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Category, Club, Fight, FightSide, GlobalClubScoringResponse, Athlete, Tatami, TatamiQueue, Tournament, TournamentOverviewStats } from '../../core/models';
import { I18nService } from '../../core/i18n.service';
import { SideThemeService } from '../../core/side-theme.service';
import { TimeService } from '../../core/time.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { canAutoRotateHero, nextHeroIndex } from './overview-hero-state';

interface TatamiView {
  tatami: Tatami;
  queue: TatamiQueue | null;
}

interface HeroFight {
  tatami: Tatami;
  fight: Fight;
}

interface QueueFight {
  tatami: Tatami;
  fight: Fight;
}

@Component({
  selector: 'app-tournament-overview',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './tournament-overview.component.html',
  styleUrl: './tournament-overview.component.css',
})
export class TournamentOverviewComponent implements OnDestroy {
  private readonly api = inject(ApiService);
  private readonly i18n = inject(I18nService);
  private readonly time = inject(TimeService);
  private readonly hub = inject(TournamentHubService);
  protected readonly context = inject(TournamentContextService);
  protected readonly sideTheme = inject(SideThemeService);

  protected readonly tournamentId = this.context.tournamentId;
  protected readonly tournament = this.context.tournament;
  protected readonly athletes = signal<Map<string, Athlete>>(new Map());
  protected readonly clubs = signal<Map<string, Club>>(new Map());
  protected readonly categories = signal<Map<string, Category>>(new Map());
  protected readonly tatamiViews = signal<TatamiView[]>([]);
  protected readonly stats = signal<TournamentOverviewStats | null>(null);
  protected readonly clubScoring = signal<GlobalClubScoringResponse | null>(null);
  protected readonly nowEpochMs = signal(Date.now());
  protected readonly heroIndex = signal(0);
  protected readonly heroHoverPaused = signal(false);
  protected readonly heroFocusPaused = signal(false);
  protected readonly heroInteractionPaused = computed(() =>
    this.heroHoverPaused() || this.heroFocusPaused());
  protected readonly reducedMotion = signal(this.detectReducedMotion());
  protected readonly hubConnected = this.hub.connected;
  protected readonly loading = signal(true);

  protected readonly activeTatamiViews = computed(() =>
    this.tatamiViews().filter((view) => view.tatami.isActive));

  protected readonly runningFights = computed<HeroFight[]>(() =>
    this.activeTatamiViews()
      .map((view) => {
        const fight = view.queue?.upcoming.find((candidate) => this.isFightRunning(candidate));
        return fight ? { tatami: view.tatami, fight } : null;
      })
      .filter((entry): entry is HeroFight => entry !== null));

  protected readonly heroFight = computed(() => {
    const fights = this.runningFights();
    return fights[this.heroIndex()] ?? fights[0] ?? null;
  });

  protected readonly heroAutoRotateAllowed = computed(() =>
    canAutoRotateHero({
      reducedMotion: this.reducedMotion(),
      interactionPaused: this.heroInteractionPaused(),
      osaeKomiActive: this.isOsaeKomiActive(this.heroFight()?.fight ?? null),
    }));

  protected readonly nextFights = computed<QueueFight[]>(() =>
    this.activeTatamiViews()
      .flatMap((view) => (view.queue?.upcoming ?? [])
        .filter((fight) => fight.status === 'Pending' && !fight.isBye)
        .map((fight) => ({ tatami: view.tatami, fight })))
      .slice(0, 8));

  protected readonly completionPercent = computed(() => {
    const value = this.stats();
    if (!value || value.fightsTotal === 0) {
      return 0;
    }

    return Math.round((value.fightsCompleted / value.fightsTotal) * 100);
  });

  private dataSubscriptions = new Subscription();
  private fightSubscription?: Subscription;
  private categoryFightsSubscription?: Subscription;
  private serverTimeSubscription?: Subscription;
  private reconnectSubscription?: Subscription;
  private clockHandle: ReturnType<typeof setInterval> | null = null;
  private heroRotationHandle: ReturnType<typeof setInterval> | null = null;
  private refreshHandle: ReturnType<typeof setTimeout> | null = null;

  private readonly activeTournamentEffect = effect((onCleanup) => {
    const tournamentId = this.context.tournamentId();
    this.dataSubscriptions.unsubscribe();
    this.dataSubscriptions = new Subscription();

    if (!tournamentId) {
      this.loading.set(false);
      this.tatamiViews.set([]);
      this.stats.set(null);
      this.clubScoring.set(null);
      onCleanup(() => undefined);
      return;
    }

    untracked(() => this.loadData(tournamentId));
    onCleanup(() => {
      this.dataSubscriptions.unsubscribe();
      this.clearRefreshHandle();
    });
  });

  constructor() {
    this.startClock();
    this.fightSubscription = this.hub.fightUpdated$.subscribe((fight) => {
      const tournamentId = this.tournamentId();
      if (tournamentId && fight.tournamentId === tournamentId) {
        this.scheduleRefresh(tournamentId);
      }
    });
    this.categoryFightsSubscription = this.hub.categoryFightsUpdated$.subscribe((event) => {
      const tournamentId = this.tournamentId();
      if (tournamentId && event.tournamentId === tournamentId) {
        this.scheduleRefresh(tournamentId);
      }
    });
    this.serverTimeSubscription = this.hub.serverTimeSync$.subscribe((serverNowUtc) => {
      this.time.ingestServerNowUtc(serverNowUtc);
    });
    this.reconnectSubscription = this.hub.reconnected$.subscribe(() => {
      const tournamentId = this.tournamentId();
      if (tournamentId) {
        void this.time.synchronize(5);
        this.scheduleRefresh(tournamentId);
      }
    });
  }

  ngOnDestroy(): void {
    this.dataSubscriptions.unsubscribe();
    this.fightSubscription?.unsubscribe();
    this.categoryFightsSubscription?.unsubscribe();
    this.serverTimeSubscription?.unsubscribe();
    this.reconnectSubscription?.unsubscribe();
    this.clearRefreshHandle();
    if (this.clockHandle !== null) {
      clearInterval(this.clockHandle);
      this.clockHandle = null;
    }
    if (this.heroRotationHandle !== null) {
      clearInterval(this.heroRotationHandle);
      this.heroRotationHandle = null;
    }
  }

  protected previousHero(): void {
    this.moveHero(-1);
  }

  protected nextHero(): void {
    this.moveHero(1);
  }

  protected selectHero(index: number): void {
    if (index >= 0 && index < this.runningFights().length) {
      this.heroIndex.set(index);
    }
  }

  protected setHeroHoverPaused(paused: boolean): void {
    this.heroHoverPaused.set(paused);
  }

  protected setHeroFocusPaused(paused: boolean): void {
    this.heroFocusPaused.set(paused);
  }

  protected onHeroFocusOut(event: FocusEvent): void {
    const container = event.currentTarget;
    if (container instanceof HTMLElement && event.relatedTarget instanceof Node && container.contains(event.relatedTarget)) {
      return;
    }

    this.heroFocusPaused.set(false);
  }

  protected statusKey(view: TatamiView): string {
    const fight = view.queue?.current;
    if (!fight || !this.isFightRunning(fight)) {
      return 'tournamentOverview.statusFree';
    }

    return this.isOsaeKomiActive(fight)
      ? 'tournamentOverview.statusOsaeKomi'
      : 'tournamentOverview.statusRunning';
  }

  protected statusClass(view: TatamiView): string {
    const fight = view.queue?.current;
    if (!fight || !this.isFightRunning(fight)) {
      return 'status--free';
    }

    return this.isOsaeKomiActive(fight) ? 'status--osae' : 'status--running';
  }

  protected currentFight(view: TatamiView): Fight | null {
    return view.queue?.current ?? null;
  }

  protected progressPercent(fight: Fight | null): number {
    if (!fight || !this.isFightRunning(fight) || !fight.startedAtUtc) {
      return 0;
    }

    const category = this.categories().get(fight.categoryId);
    const duration = category?.matchDurationSeconds ?? 300;
    const elapsed = this.elapsedFightSeconds(fight);
    return Math.min(100, Math.max(0, (elapsed / Math.max(1, duration)) * 100));
  }

  protected timerForFight(fight: Fight): string {
    this.nowEpochMs();
    const category = this.categories().get(fight.categoryId);
    const matchDuration = category?.matchDurationSeconds ?? 300;
    const goldenScoreDuration = category?.goldenScoreDurationSeconds ?? 180;

    if (!fight.startedAtUtc) {
      return this.formatWholeSeconds(matchDuration);
    }

    const elapsed = this.elapsedFightSeconds(fight);
    const remaining = fight.isGoldenScore
      ? goldenScoreDuration - Math.max(0, elapsed - matchDuration)
      : matchDuration - elapsed;
    return this.formatWholeSeconds(remaining);
  }

  protected osaeKomiLabel(fight: Fight): string {
    if (!this.isOsaeKomiActive(fight)) {
      return '--';
    }

    const side = fight.osaeKomiSide === 'White' ? 'white' : 'blue';
    const cap = this.hasWazaAri(fight, side)
      ? this.tournament()?.osaeKomiWazaAriSeconds ?? 10
      : this.tournament()?.osaeKomiIpponSeconds ?? 20;
    const reference = fight.osaeKomiPausedAtUtc
      ? new Date(fight.osaeKomiPausedAtUtc).getTime()
      : this.nowEpochMs();
    const activeMilliseconds = fight.osaeKomiStartedAtUtc
      ? Math.max(0, reference - new Date(fight.osaeKomiStartedAtUtc).getTime())
      : 0;
    const seconds = Math.min(cap, Math.max(0, (fight.osaeKomiElapsedMilliseconds + activeMilliseconds) / 1000));
    return `${seconds.toFixed(1)} s`;
  }

  protected athleteName(id: string | null): string {
    if (!id) {
      return this.i18n.translate('draw.tbd');
    }

    const athlete = this.athletes().get(id);
    return athlete ? `${athlete.lastName}, ${athlete.firstName}` : this.i18n.translate('draw.tbd');
  }

  protected athleteClub(id: string | null): string {
    const athlete = id ? this.athletes().get(id) : undefined;
    return athlete ? this.clubs().get(athlete.clubId)?.name ?? '' : '';
  }

  protected categoryName(id: string): string {
    return this.categories().get(id)?.name ?? this.i18n.translate('draw.tbd');
  }

  protected scoreCount(fight: Fight, side: FightSide, score: 'ippon' | 'wazaAri' | 'shido'): number {
    if (side === 'white') {
      return score === 'ippon' ? fight.whiteIpponCount
        : score === 'wazaAri' ? fight.whiteWazaAriCount
          : Math.min(3, fight.whitePenalties);
    }

    return score === 'ippon' ? fight.blueIpponCount
      : score === 'wazaAri' ? fight.blueWazaAriCount
        : Math.min(3, fight.bluePenalties);
  }

  protected shidoSlots(): number[] {
    return [0, 1, 2];
  }

  protected formatAverageDuration(seconds: number | null): string {
    if (seconds === null) {
      return this.i18n.translate('tournamentOverview.noData');
    }

    return this.formatWholeSeconds(seconds);
  }

  protected formatDurationSeconds(seconds: number): string {
    return this.formatWholeSeconds(seconds);
  }

  protected sideLabel(side: FightSide): string {
    return this.i18n.translate(this.sideTheme.sideLabelKey(side, this.tournament()));
  }

  protected isOsaeKomiActive(fight: Fight | null): boolean {
    return fight?.osaeKomiSide !== null
      && fight?.osaeKomiSide !== undefined
      && (fight.osaeKomiStartedAtUtc !== null || fight.osaeKomiPausedAtUtc !== null);
  }

  protected isFightRunning(fight: Fight): boolean {
    return fight.status === 'InProgress' || fight.status === 'Paused';
  }

  protected isPaused(fight: Fight): boolean {
    return fight.status === 'Paused';
  }

  private loadData(tournamentId: string): void {
    this.loading.set(true);
    this.stats.set(null);
    this.clubScoring.set(null);
    void this.hub.connect(tournamentId);

    this.dataSubscriptions.add(this.api.getTournament(tournamentId).subscribe({
      next: (tournament) => this.sideTheme.applyTheme(document.documentElement, tournament),
      error: () => undefined,
    }));
    this.dataSubscriptions.add(this.api.getAthletes(tournamentId).subscribe({
      next: (athletes) => this.athletes.set(new Map(athletes.map((athlete) => [athlete.id, athlete]))),
      error: () => undefined,
    }));
    this.dataSubscriptions.add(this.api.getClubs(tournamentId).subscribe({
      next: (clubs) => this.clubs.set(new Map(clubs.map((club) => [club.id, club]))),
      error: () => undefined,
    }));
    this.dataSubscriptions.add(this.api.getCategories(tournamentId).subscribe({
      next: (categories) => this.categories.set(new Map(categories.map((category) => [category.id, category]))),
      error: () => undefined,
    }));
    this.refreshSnapshot(tournamentId);
    this.refreshQueues(tournamentId);
  }

  private refreshSnapshot(tournamentId: string): void {
    this.dataSubscriptions.add(this.api.getTournamentOverviewStats(tournamentId).subscribe({
      next: (stats) => this.stats.set(stats),
      error: () => undefined,
    }));
    this.dataSubscriptions.add(this.api.getGlobalClubScoring(tournamentId).subscribe({
      next: (scoring) => this.clubScoring.set(scoring),
      error: () => undefined,
    }));
  }

  private refreshQueues(tournamentId: string): void {
    this.dataSubscriptions.add(this.api.getTatamis(tournamentId).pipe(
      map((tatamis) => [...tatamis]
        .filter((tatami) => tatami.isActive)
        .sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))),
      switchMap((tatamis) => tatamis.length === 0
        ? of([] as TatamiView[])
        : forkJoin(tatamis.map((tatami) => this.api.getTatamiQueue(tournamentId, tatami.id).pipe(
          map((queue) => ({ tatami, queue })),
          catchError(() => of({ tatami, queue: null })),
        )))),
    ).subscribe({
      next: (views) => {
        this.applyTatamiViews(views);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    }));
  }

  private applyTatamiViews(views: TatamiView[]): void {
    const selectedFightId = this.heroFight()?.fight.id ?? null;
    this.tatamiViews.set(views);
    const nextIndex = selectedFightId
      ? this.runningFights().findIndex((entry) => entry.fight.id === selectedFightId)
      : -1;
    this.heroIndex.set(nextIndex >= 0 ? nextIndex : 0);
  }

  private scheduleRefresh(tournamentId: string): void {
    if (this.refreshHandle !== null) {
      clearTimeout(this.refreshHandle);
    }

    this.refreshHandle = setTimeout(() => {
      this.refreshHandle = null;
      this.refreshQueues(tournamentId);
      this.refreshSnapshot(tournamentId);
    }, 300);
  }

  private clearRefreshHandle(): void {
    if (this.refreshHandle !== null) {
      clearTimeout(this.refreshHandle);
      this.refreshHandle = null;
    }
  }

  private moveHero(direction: 1 | -1): void {
    this.heroIndex.set(nextHeroIndex(this.heroIndex(), this.runningFights().length, direction));
  }

  private startClock(): void {
    void this.time.synchronize(5);
    this.clockHandle = setInterval(() => {
      this.nowEpochMs.set(this.time.nowMs());
    }, 250);
    this.heroRotationHandle = setInterval(() => {
      if (this.runningFights().length > 1 && this.heroAutoRotateAllowed()) {
        this.moveHero(1);
      }
    }, 9_000);
  }

  private elapsedFightSeconds(fight: Fight): number {
    if (!fight.startedAtUtc) {
      return 0;
    }

    const reference = fight.osaeKomiPausedAtUtc
      ? new Date(fight.osaeKomiPausedAtUtc).getTime()
      : fight.status === 'Paused' && fight.pausedAtUtc
        ? new Date(fight.pausedAtUtc).getTime()
        : this.nowEpochMs();
    return Math.max(0, (reference - new Date(fight.startedAtUtc).getTime()) / 1000);
  }

  private hasWazaAri(fight: Fight, side: 'white' | 'blue'): boolean {
    return side === 'white' ? fight.whiteWazaAriCount > 0 : fight.blueWazaAriCount > 0;
  }

  private formatWholeSeconds(seconds: number): string {
    const rounded = Math.max(0, Math.ceil(seconds));
    return `${Math.floor(rounded / 60).toString().padStart(2, '0')}:${(rounded % 60).toString().padStart(2, '0')}`;
  }

  private detectReducedMotion(): boolean {
    return typeof window !== 'undefined'
      && typeof window.matchMedia === 'function'
      && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }
}
