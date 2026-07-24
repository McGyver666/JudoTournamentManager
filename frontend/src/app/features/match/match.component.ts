import {
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { SideThemeService } from '../../core/side-theme.service';
import { TimeService } from '../../core/time.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { TranslatePipe } from '../../core/translate.pipe';
import {
  AdjustScoreRequest,
  Athlete,
  Category,
  Club,
  Fight,
  FightSide,
  ScoreType,
  Tatami,
  TatamiQueue,
  OsaeKomiRequest,
} from '../../core/models';

const OPERATOR_NAME_KEY = 'judo.operatorName';

interface WinnerConfirmationState {
  fight: Fight;
  winnerId: string;
  nextFight: Fight | null;
}

@Component({
  selector: 'app-match',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './match.component.html',
  styleUrl: './match.component.css',
})
export class MatchComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  protected readonly sideTheme = inject(SideThemeService);
  protected readonly context = inject(TournamentContextService);
  private readonly hub = inject(TournamentHubService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly canOperate = this.auth.canOperate;
  private readonly time = inject(TimeService);

  protected readonly tatamis = signal<Tatami[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly athletes = signal<Map<string, Athlete>>(new Map());
  protected readonly clubs = signal<Map<string, Club>>(new Map());
  protected readonly selectedTatamiId = signal<string | null>(null);
  protected readonly queue = signal<TatamiQueue | null>(null);
  protected readonly operatorName = signal<string>(this.restoreOperatorName());
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly winnerConfirmation = signal<WinnerConfirmationState | null>(null);
  protected readonly confirmingWinner = signal(false);
  protected readonly nowEpochMs = signal<number>(Date.now());

  /** Remaining seconds in the current fight's countdown. */
  protected readonly timerSeconds = signal<number | null>(null);
  protected readonly timerRemainingExactSeconds = signal<number | null>(null);
  protected readonly timerIsGoldenScore = signal(false);
  protected readonly osaeKomiSeconds = signal<number | null>(null);
  protected readonly osaeKomiElapsedExactSeconds = signal<number | null>(null);
  protected readonly osaeKomiCapSeconds = signal<number | null>(null);
  protected readonly osaeKomiSide = signal<FightSide | null>(null);
  private timerHandle: ReturnType<typeof setInterval> | null = null;
  private lastClockResyncCheckAtMs = 0;

  /** Track the previous fight to detect osae-komi transitions and cleanup. */
  private previousFight: Fight | null = null;
  /** When osae-komi ends, store the frozen display until a new one starts or fight resumes. */
  private frozenOsaeKomiDisplay: { seconds: number; cap: number; side: FightSide } | null = null;

  protected readonly scoreTypes: ScoreType[] = ['Ippon', 'WazaAri', 'Yuko', 'Shido'];

  protected readonly hubConnected = computed(() => this.hub.connected());
  protected readonly currentFight = computed(() => this.queue()?.current ?? null);
  /** All pending fights assigned to the tatami, excluding the current fight, in queue order. */
  protected readonly upcomingFights = computed(() => {
    const q = this.queue();
    if (!q) return [] as Fight[];
    const currentId = q.current?.id;
    return q.upcoming.filter((fight) => fight.id !== currentId);
  });
  protected readonly currentTimeLabel = computed(() => this.formatCurrentTime(this.nowEpochMs()));

  private fightSub?: Subscription;
  private serverTimeSub?: Subscription;
  private reconnectSub?: Subscription;
  private querySub?: Subscription;
  private headerClockHandle: ReturnType<typeof setInterval> | null = null;

  private readonly selectedTatamiEffect = effect(() => {
    const tournamentId = this.context.tournamentId();
    const tatamiId = this.selectedTatamiId();

    if (!tournamentId || !tatamiId) {
      this.queue.set(null);
      this.stopTimer();
      this.previousFight = null;
      this.frozenOsaeKomiDisplay = null;
      return;
    }

    this.refreshQueue();
  });

  ngOnInit(): void {
    void this.time.synchronize(5);
    this.startHeaderClock();

    const tid = this.context.tournamentId();
    if (!tid) return;

    this.querySub = this.route.queryParamMap.subscribe((params) => {
      const tatamiId = params.get('tatamiId');
      this.selectedTatamiId.set(tatamiId);
    });

    this.api.getTournament(tid).subscribe((tournament) => {
      this.context.refreshIfActive(tournament);
    });

    this.api.getTatamis(tid).subscribe((tatamis) => {
      const sortedTatamis = [...tatamis].sort(
        (a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name),
      );
      this.tatamis.set(sortedTatamis);

      if (sortedTatamis.length === 0) {
        this.selectedTatamiId.set(null);
        return;
      }

      const selectedTatamiId = this.selectedTatamiId();
      const selectedTatamiExists = selectedTatamiId
        ? sortedTatamis.some((tatami) => tatami.id === selectedTatamiId)
        : false;

      if (!selectedTatamiExists) {
        void this.router.navigate([], {
          relativeTo: this.route,
          queryParams: {
            tournamentId: tid,
            tatamiId: sortedTatamis[0].id,
          },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
      }
    });
    this.api.getCategories(tid).subscribe(cats => this.categories.set(cats));
    this.api.getAthletes(tid).subscribe(athletes => {
      const map = new Map(athletes.map(a => [a.id, a]));
      this.athletes.set(map);
    });
    this.api.getClubs(tid).subscribe(clubs => {
      const map = new Map(clubs.map(club => [club.id, club]));
      this.clubs.set(map);
    });

    this.fightSub = this.hub.fightUpdated$.subscribe(fight => {
      const q = this.queue();
      if (!q) return;
      // Refresh whenever an updated fight belongs to the selected tatami so queue reordering,
      // current/next/on-deck changes and score updates stay in sync across clients.
      if (fight.tatamiId === this.selectedTatamiId()
        || q.current?.id === fight.id || q.next?.id === fight.id || q.onDeck?.id === fight.id) {
        this.refreshQueue();
      }
    });

    this.serverTimeSub = this.hub.serverTimeSync$.subscribe((serverNowUtc) => {
      this.time.ingestServerNowUtc(serverNowUtc);
    });

    this.reconnectSub = this.hub.reconnected$.subscribe(() => {
      void this.time.synchronize(5);
    });
  }

  ngOnDestroy(): void {
    this.querySub?.unsubscribe();
    this.fightSub?.unsubscribe();
    this.serverTimeSub?.unsubscribe();
    this.reconnectSub?.unsubscribe();
    this.stopTimer();
    this.stopHeaderClock();
  }

  @HostListener('document:visibilitychange')
  protected onVisibilityChange(): void {
    if (document.visibilityState === 'visible') {
      void this.time.synchronizeIfStale();
    }
  }

  protected refreshQueue(): void {
    const tid = this.context.tournamentId();
    const mid = this.selectedTatamiId();
    if (!tid || !mid) return;

    this.api.getTatamiQueue(tid, mid).subscribe({
      next: q => {
        this.queue.set(q);
        this.restartTimer(q.current);
      },
      error: () => this.errorMessage.set('Fehler beim Laden der Warteschlange.'),
    });
  }

  private restartTimer(fight: Fight | null): void {
    this.stopTimer();

    // Detect osae-komi transitions before resetting state.
    if (this.previousFight !== null && fight !== null) {
      const prevHadOsaeKomi = this.previousFight.osaeKomiSide !== null;
      const newHasOsaeKomi = fight.osaeKomiSide !== null;
      const wasPausedNowRunning = this.previousFight.status === 'Paused' && fight.status === 'InProgress';
      const isNowPaused = fight.status === 'Paused';

      // Osae-komi was active and is now stopped: freeze the display (but not if fight is being paused or just resumed).
      if (prevHadOsaeKomi && !newHasOsaeKomi && !isNowPaused && !wasPausedNowRunning) {
        const s = this.osaeKomiSeconds();
        const c = this.osaeKomiCapSeconds();
        const side = this.osaeKomiSide();
        if (s !== null && c !== null && side !== null) {
          this.frozenOsaeKomiDisplay = { seconds: s, cap: c, side };
        }
      }

      // New osae-komi started: clear frozen display.
      if (newHasOsaeKomi) {
        this.frozenOsaeKomiDisplay = null;
      }

      // Fight was resumed: clear frozen display.
      if (wasPausedNowRunning) {
        this.frozenOsaeKomiDisplay = null;
      }
    }

    this.previousFight = fight;

    // Not started: show configured duration.
    if (!fight || !fight.startedAtUtc) {
      const categoryId = fight?.categoryId;
      const cat = categoryId ? this.categories().find(c => c.id === categoryId) : null;
      const duration = cat?.matchDurationSeconds ?? 300;
      this.timerSeconds.set(duration);
      this.timerRemainingExactSeconds.set(duration);
      this.timerIsGoldenScore.set(false);
      this.osaeKomiSeconds.set(null);
      this.osaeKomiElapsedExactSeconds.set(null);
      this.osaeKomiCapSeconds.set(null);
      this.osaeKomiSide.set(null);
      return;
    }

    const categoryId = fight.categoryId;
    const cat = this.categories().find(c => c.id === categoryId);
    const duration = cat?.matchDurationSeconds ?? 300;
    const goldenScoreDuration = cat?.goldenScoreDurationSeconds ?? 180;

    const tick = () => {
      const nowMs = Date.now();
      if (nowMs - this.lastClockResyncCheckAtMs >= 10_000) {
        this.lastClockResyncCheckAtMs = nowMs;
        void this.time.synchronizeIfStale();
      }

      const timerReferenceMs = fight.status === 'Paused' && fight.pausedAtUtc
        ? new Date(fight.pausedAtUtc).getTime()
        : this.time.nowMs();
      const elapsed = (timerReferenceMs - new Date(fight.startedAtUtc!).getTime()) / 1000;
      const regularRemaining = Math.max(0, duration - elapsed);

      // Use fight.isGoldenScore from server (reload-safe, score-aware).
      if (fight.isGoldenScore) {
        this.timerIsGoldenScore.set(true);
        const goldenElapsed = Math.max(0, elapsed - duration);
        const goldenRemaining = Math.max(0, goldenScoreDuration - goldenElapsed);
        this.timerRemainingExactSeconds.set(goldenRemaining);
        this.timerSeconds.set(Math.ceil(goldenRemaining));
      } else {
        // Regular time countdown.
        this.timerIsGoldenScore.set(false);
        this.timerRemainingExactSeconds.set(regularRemaining);
        this.timerSeconds.set(Math.ceil(regularRemaining));
      }

      // Update osae-komi display or use frozen display if stopped.
      if (fight.osaeKomiSide && fight.osaeKomiStartedAtUtc) {
        const side = fight.osaeKomiSide === 'White' ? 'white' : 'blue';
        const cap = this.getOsaeKomiCap(fight, side);
        const holdExactSeconds = Math.max(0, Math.min(cap, (this.time.nowMs() - new Date(fight.osaeKomiStartedAtUtc).getTime()) / 1000));
        const holdSeconds = Math.min(cap, Math.ceil(holdExactSeconds));
        this.osaeKomiSeconds.set(holdSeconds);
        this.osaeKomiElapsedExactSeconds.set(holdExactSeconds);
        this.osaeKomiCapSeconds.set(cap);
        this.osaeKomiSide.set(side);
        this.frozenOsaeKomiDisplay = null; // Active hold, no frozen display.
      } else {
        // No active osae-komi: clear signals (display will use frozen fallback).
        // This allows start buttons to be re-enabled after stopping osae-komi.
        this.osaeKomiSeconds.set(null);
        this.osaeKomiElapsedExactSeconds.set(null);
        this.osaeKomiCapSeconds.set(null);
        this.osaeKomiSide.set(null);
      }
    };

    tick();
    this.timerHandle = setInterval(tick, 100);
  }

  private stopTimer(): void {
    if (this.timerHandle !== null) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
    }
  }

  private startHeaderClock(): void {
    this.refreshCurrentTime();
    this.headerClockHandle = setInterval(() => {
      this.refreshCurrentTime();
    }, 1000);
  }

  private stopHeaderClock(): void {
    if (this.headerClockHandle !== null) {
      clearInterval(this.headerClockHandle);
      this.headerClockHandle = null;
    }
  }

  private refreshCurrentTime(): void {
    const localNowMs = Date.now();
    if (localNowMs - this.lastClockResyncCheckAtMs >= 10_000) {
      this.lastClockResyncCheckAtMs = localNowMs;
      void this.time.synchronizeIfStale();
    }

    this.nowEpochMs.set(this.time.nowMs());
  }

  private formatCurrentTime(epochMs: number): string {
    return new Intl.DateTimeFormat('de-DE', {
      hour: '2-digit',
      minute: '2-digit',
    }).format(epochMs);
  }

  private formatWholeSeconds(seconds: number): string {
    const rounded = Math.max(0, Math.ceil(seconds));
    const m = Math.floor(rounded / 60);
    const s = rounded % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  private formatTenthsCountdown(seconds: number): string {
    const clamped = Math.max(0, seconds);
    const wholeSeconds = Math.floor(clamped);
    const tenths = Math.floor((clamped - wholeSeconds) * 10);
    const m = Math.floor(wholeSeconds / 60);
    const s = wholeSeconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}.${tenths}`;
  }

  private formatFightDuration(totalSeconds: number): string {
    const normalized = Math.max(0, Math.floor(totalSeconds));
    const minutes = Math.floor(normalized / 60);
    const seconds = normalized % 60;
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }

  protected athleteName(id: string | null): string {
    if (!id) return '?';
    const a = this.athletes().get(id);
    return a ? `${a.lastName}, ${a.firstName}` : id.substring(0, 8);
  }

  protected athleteClubName(id: string | null): string | null {
    if (!id) return null;
    const athlete = this.athletes().get(id);
    if (!athlete) return null;
    return this.clubs().get(athlete.clubId)?.name ?? null;
  }

  protected lastFightInfoLabel(id: string | null): string | null {
    if (!id) return null;
    const athlete = this.athletes().get(id);
    if (!athlete?.lastFightEndedAtUtc || athlete.lastFightDurationSeconds === null) {
      return null;
    }

    const endedAt = new Date(athlete.lastFightEndedAtUtc);
    if (Number.isNaN(endedAt.getTime())) {
      return null;
    }

    const timeLabel = new Intl.DateTimeFormat('de-DE', {
      hour: '2-digit',
      minute: '2-digit',
    }).format(endedAt);

    return `${timeLabel} (${this.formatFightDuration(athlete.lastFightDurationSeconds)})`;
  }

  protected hasInsufficientRest(id: string | null): boolean {
    if (!id) return false;

    const athlete = this.athletes().get(id);
    if (!athlete?.lastFightEndedAtUtc) {
      return false;
    }

    const minimumGapSeconds = this.context.tournament()?.minimumRestBetweenFightsSeconds ?? 0;
    if (minimumGapSeconds <= 0) {
      return false;
    }

    const endedAtMs = new Date(athlete.lastFightEndedAtUtc).getTime();
    if (Number.isNaN(endedAtMs)) {
      return false;
    }

    const elapsedSeconds = Math.floor((this.nowEpochMs() - endedAtMs) / 1000);
    return elapsedSeconds >= 0 && elapsedSeconds < minimumGapSeconds;
  }

  protected categoryName(id: string): string {
    return this.categories().find((category) => category.id === id)?.name ?? id;
  }

  protected canMoveUp(index: number): boolean {
    return this.canOperate() && index > 0;
  }

  protected canMoveDown(index: number): boolean {
    return this.canOperate() && index < this.upcomingFights().length - 1;
  }

  protected moveFightUp(fight: Fight, index: number): void {
    if (!this.canMoveUp(index)) return;
    this.moveFight(fight, 'Up');
  }

  protected moveFightDown(fight: Fight, index: number): void {
    if (!this.canMoveDown(index)) return;
    this.moveFight(fight, 'Down');
  }

  private moveFight(fight: Fight, direction: 'Up' | 'Down'): void {
    const tid = this.context.tournamentId();
    if (!tid) return;
    this.api.moveFightInQueue(tid, fight.id, direction).subscribe({
      next: () => this.refreshQueue(),
      error: () => this.errorMessage.set('Kampfreihenfolge konnte nicht geändert werden.'),
    });
  }

  protected startFight(fight: Fight): void {
    if (!this.canOperate()) return;
    const tid = this.context.tournamentId();
    if (!tid) return;
    this.api.startFight(tid, fight.id, this.operatorName()).subscribe({
      next: () => this.refreshQueue(),
      error: () => this.errorMessage.set('Kampf konnte nicht gestartet werden.'),
    });
  }

  protected pauseFight(fight: Fight): void {
    if (!this.canOperate()) return;
    const tid = this.context.tournamentId();
    if (!tid) return;
    this.api.pauseFight(tid, fight.id, this.operatorName()).subscribe({
      next: () => this.refreshQueue(),
      error: () => this.errorMessage.set('Kampf konnte nicht gestoppt werden.'),
    });
  }

  protected resumeFight(fight: Fight): void {
    if (!this.canOperate()) return;
    const tid = this.context.tournamentId();
    if (!tid) return;
    this.api.resumeFight(tid, fight.id, this.operatorName()).subscribe({
      next: () => this.refreshQueue(),
      error: () => this.errorMessage.set('Kampf konnte nicht fortgesetzt werden.'),
    });
  }

  protected adjustScore(fight: Fight, side: FightSide, scoreType: ScoreType, delta: number): void {
    if (!this.canOperate()) return;
    const tid = this.context.tournamentId();
    if (!tid) return;
    const body: AdjustScoreRequest = { side, scoreType, delta };
    this.api.adjustScore(tid, fight.id, body, this.operatorName()).subscribe({
      next: () => this.refreshQueue(),
      error: () => this.errorMessage.set('Wertung konnte nicht gespeichert werden.'),
    });
  }

  protected addScore(fight: Fight, side: 'white' | 'blue', points: number): void {
    if (!this.canOperate()) return;
    const tid = this.context.tournamentId();
    if (!tid) return;
    const newWhite = side === 'white' ? fight.whiteScore + points : fight.whiteScore;
    const newBlue = side === 'blue' ? fight.blueScore + points : fight.blueScore;
    this.api.recordScore(tid, fight.id,
      { whiteScore: newWhite, blueScore: newBlue, whitePenalties: fight.whitePenalties, bluePenalties: fight.bluePenalties },
      this.operatorName()).subscribe({
        next: () => this.refreshQueue(),
        error: () => this.errorMessage.set('Punkte konnten nicht gespeichert werden.'),
      });
  }

  protected addPenalty(fight: Fight, side: 'white' | 'blue'): void {
    if (!this.canOperate()) return;
    const tid = this.context.tournamentId();
    if (!tid) return;
    const newWhiteP = side === 'white' ? fight.whitePenalties + 1 : fight.whitePenalties;
    const newBlueP = side === 'blue' ? fight.bluePenalties + 1 : fight.bluePenalties;
    this.api.recordScore(tid, fight.id,
      { whiteScore: fight.whiteScore, blueScore: fight.blueScore, whitePenalties: newWhiteP, bluePenalties: newBlueP },
      this.operatorName()).subscribe({
        next: () => this.refreshQueue(),
        error: () => this.errorMessage.set('Strafe konnte nicht gespeichert werden.'),
      });
  }

    protected startOsaeKomi(fight: Fight, side: FightSide): void {
      if (!this.canOperate()) return;
      const tid = this.context.tournamentId();
      if (!tid) return;
      const body: OsaeKomiRequest = { side };
      this.api.startOsaeKomi(tid, fight.id, body, this.operatorName()).subscribe({
        next: () => this.refreshQueue(),
        error: () => this.errorMessage.set('Osae-komi konnte nicht gestartet werden.'),
      });
    }

    protected stopOsaeKomi(fight: Fight): void {
      if (!this.canOperate()) return;
      const tid = this.context.tournamentId();
      if (!tid) return;
      this.api.stopOsaeKomi(tid, fight.id, this.operatorName()).subscribe({
        next: () => this.refreshQueue(),
        error: () => this.errorMessage.set('Osae-komi konnte nicht gestoppt werden.'),
      });
    }

  protected confirmWinner(fight: Fight, winnerId: string): void {
    if (!this.canOperate() || this.confirmingWinner()) return;
    this.winnerConfirmation.set({
      fight,
      winnerId,
      nextFight: this.queue()?.next ?? null,
    });
  }

  protected cancelWinnerConfirmation(): void {
    if (this.confirmingWinner()) {
      return;
    }

    this.winnerConfirmation.set(null);
  }

  protected confirmWinnerAndLoadNext(): void {
    if (!this.canOperate() || this.confirmingWinner()) return;
    const tid = this.context.tournamentId();
    const confirmation = this.winnerConfirmation();
    if (!tid || !confirmation) return;

    this.confirmingWinner.set(true);
    this.api.confirmResult(tid, confirmation.fight.id, { winnerId: confirmation.winnerId }, this.operatorName()).subscribe({
      next: () => {
        this.winnerConfirmation.set(null);
        this.queue.set(null);
        this.refreshQueue();
        this.confirmingWinner.set(false);
      },
      error: () => {
        this.errorMessage.set('Ergebnis konnte nicht bestätigt werden.');
        this.confirmingWinner.set(false);
      },
    });
  }

  protected clearError(): void {
    this.errorMessage.set(null);
  }

  protected scoreCount(fight: Fight, side: FightSide, scoreType: ScoreType): number {
    const prefix = side === 'white' ? 'white' : 'blue';
    switch (scoreType) {
      case 'Ippon': return prefix === 'white' ? fight.whiteIpponCount : fight.blueIpponCount;
      case 'WazaAri': return prefix === 'white' ? fight.whiteWazaAriCount : fight.blueWazaAriCount;
      case 'Yuko': return prefix === 'white' ? fight.whiteYukoCount : fight.blueYukoCount;
      case 'Shido': return prefix === 'white' ? fight.whitePenalties : fight.bluePenalties;
      default: return 0;
    }
  }

  protected canRemoveScore(fight: Fight, side: FightSide, scoreType: ScoreType): boolean {
    return this.scoreCount(fight, side, scoreType) > 0;
  }

  protected canAddScore(fight: Fight, side: FightSide, scoreType: ScoreType): boolean {
    switch (scoreType) {
      case 'Ippon': return this.scoreCount(fight, side, scoreType) < 1;
      case 'WazaAri': return this.scoreCount(fight, side, scoreType) < 2;
      case 'Shido': return this.scoreCount(fight, side, scoreType) < 3;
      default: return true;
    }
  }

  protected shidoSlots(): number[] {
    return [0, 1, 2];
  }

  protected scoreLabel(scoreType: ScoreType): string {
    switch (scoreType) {
      case 'Ippon': return 'Ippon';
      case 'WazaAri': return 'Waza-ari';
      case 'Yuko': return 'Yuko';
      case 'Shido': return 'Shido';
    }
  }

  protected hasWazaAri(fight: Fight, side: FightSide): boolean {
    return side === 'white' ? fight.whiteWazaAriCount > 0 : fight.blueWazaAriCount > 0;
  }

  protected holdTimerLabel(): string {
    const seconds = this.osaeKomiSeconds() ?? this.frozenOsaeKomiDisplay?.seconds ?? null;
    const exactSeconds = this.osaeKomiElapsedExactSeconds();
    const cap = this.osaeKomiCapSeconds() ?? this.frozenOsaeKomiDisplay?.cap ?? 25;
    if (seconds === null) return '--s';

    const isRunning = this.osaeKomiSide() !== null && exactSeconds !== null;
    const remainingToCap = exactSeconds !== null ? Math.max(0, cap - exactSeconds) : Number.MAX_VALUE;
    const showTenths = isRunning && remainingToCap <= 10;
    const primary = showTenths && exactSeconds !== null
      ? `${exactSeconds.toFixed(1)}s`
      : `${seconds}s`;

    return `${primary} / ${cap}s`;
  }

  protected fightTimerLabel(): string {
    const fight = this.currentFight();
    const remainingExact = this.timerRemainingExactSeconds();
    if (!fight || remainingExact === null) {
      return '--:--';
    }

    const showTenths = fight.status === 'InProgress' && remainingExact <= 10;
    return showTenths
      ? this.formatTenthsCountdown(remainingExact)
      : this.formatWholeSeconds(remainingExact);
  }

  protected isPaused(fight: Fight): boolean {
    return fight.status === 'Paused';
  }

  protected isRunning(fight: Fight): boolean {
    return fight.status === 'InProgress';
  }

  protected canStartOsaeKomi(fight: Fight, side: FightSide): boolean {
    return fight.status === 'InProgress' && this.osaeKomiSide() === null;
  }

  protected sideLabelKey(side: FightSide): string {
    return this.sideTheme.sideLabelKey(side, this.context.tournament());
  }

  protected startOsaeLabelKey(): string {
    return this.sideTheme.startOsaeLabelKey(this.context.tournament());
  }

  protected confirmWinnerLabelKey(): string {
    return this.sideTheme.confirmWinnerLabelKey(this.context.tournament());
  }

  protected canRecordScore(fight: Fight): boolean {
    return fight.status === 'InProgress' || fight.status === 'Paused';
  }

  private getOsaeKomiCap(fight: Fight, side: FightSide): number {
    const t = this.context.tournament();
    if (this.hasWazaAri(fight, side)) {
      return t?.osaeKomiWazaAriSeconds ?? 10;
    }
    return t?.osaeKomiIpponSeconds ?? 20;
  }

  private restoreOperatorName(): string {
    try { return localStorage.getItem(OPERATOR_NAME_KEY) ?? 'Operator'; } catch { return 'Operator'; }
  }
}
