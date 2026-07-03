import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { SideThemeService } from '../../core/side-theme.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { TranslatePipe } from '../../core/translate.pipe';
import {
  AdjustScoreRequest,
  Athlete,
  Category,
  Fight,
  FightSide,
  ScoreType,
  Tatami,
  TatamiQueue,
  OsaeKomiRequest,
} from '../../core/models';

const OPERATOR_NAME_KEY = 'judo.operatorName';

@Component({
  selector: 'app-match',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './match.component.html',
  styleUrl: './match.component.css',
})
export class MatchComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  protected readonly sideTheme = inject(SideThemeService);
  protected readonly context = inject(TournamentContextService);
  private readonly hub = inject(TournamentHubService);
  protected readonly canOperate = this.auth.canOperate;

  protected readonly tatamis = signal<Tatami[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly athletes = signal<Map<string, Athlete>>(new Map());
  protected readonly selectedTatamiId = signal<string | null>(null);
  protected readonly queue = signal<TatamiQueue | null>(null);
  protected readonly operatorName = signal<string>(this.restoreOperatorName());
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly loading = signal(false);

  /** Remaining seconds in the current fight's countdown. */
  protected readonly timerSeconds = signal<number | null>(null);
  protected readonly timerIsGoldenScore = signal(false);
  protected readonly osaeKomiSeconds = signal<number | null>(null);
  protected readonly osaeKomiCapSeconds = signal<number | null>(null);
  protected readonly osaeKomiSide = signal<FightSide | null>(null);
  private timerHandle: ReturnType<typeof setInterval> | null = null;
  private autoPauseInFlightFightId: string | null = null;

  /** Track the previous fight to detect osae-komi transitions and cleanup. */
  private previousFight: Fight | null = null;
  /** When osae-komi ends, store the frozen display until a new one starts or fight resumes. */
  private frozenOsaeKomiDisplay: { seconds: number; cap: number; side: FightSide } | null = null;

  protected readonly scoreTypes: ScoreType[] = ['Ippon', 'WazaAri', 'Yuko', 'Shido'];

  protected readonly hubConnected = computed(() => this.hub.connected());
  protected readonly currentFight = computed(() => this.queue()?.current ?? null);

  private fightSub?: Subscription;

  ngOnInit(): void {
    const tid = this.context.tournamentId();
    if (!tid) return;

    this.api.getTournament(tid).subscribe((tournament) => {
      this.context.refreshIfActive(tournament);
    });

    this.api.getTatamis(tid).subscribe(t => this.tatamis.set(t));
    this.api.getCategories(tid).subscribe(cats => this.categories.set(cats));
    this.api.getAthletes(tid).subscribe(athletes => {
      const map = new Map(athletes.map(a => [a.id, a]));
      this.athletes.set(map);
    });

    this.fightSub = this.hub.fightUpdated$.subscribe(fight => {
      const q = this.queue();
      if (!q) return;
      // If the updated fight is the current fight, refresh the queue.
      if (q.current?.id === fight.id || q.next?.id === fight.id || q.onDeck?.id === fight.id) {
        this.refreshQueue();
      }
    });
  }

  ngOnDestroy(): void {
    this.fightSub?.unsubscribe();
    this.stopTimer();
  }

  protected selectTatami(tatamiId: string): void {
    this.selectedTatamiId.set(tatamiId);
    this.refreshQueue();
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
    this.autoPauseInFlightFightId = null;

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
      this.timerIsGoldenScore.set(false);
      this.osaeKomiSeconds.set(null);
      this.osaeKomiCapSeconds.set(null);
      this.osaeKomiSide.set(null);
      return;
    }

    const categoryId = fight.categoryId;
    const cat = this.categories().find(c => c.id === categoryId);
    const duration = cat?.matchDurationSeconds ?? 300;
    const goldenScoreDuration = cat?.goldenScoreDurationSeconds ?? 180;

    const tick = () => {
      const timerReference = fight.status === 'Paused' && fight.pausedAtUtc ? fight.pausedAtUtc : new Date();
      const elapsed = (new Date(timerReference).getTime() - new Date(fight.startedAtUtc!).getTime()) / 1000;
      const regularRemaining = Math.max(0, duration - elapsed);

      // Use fight.isGoldenScore from server (reload-safe, score-aware).
      if (fight.isGoldenScore) {
        this.timerIsGoldenScore.set(true);
        if (fight.status === 'Paused') {
          this.timerSeconds.set(goldenScoreDuration);
        } else {
          const goldenElapsed = Math.max(0, elapsed - duration);
          const goldenRemaining = Math.max(0, goldenScoreDuration - goldenElapsed);
          this.timerSeconds.set(Math.ceil(goldenRemaining));
        }
      } else {
        // Regular time countdown.
        this.timerIsGoldenScore.set(false);
        this.timerSeconds.set(Math.ceil(regularRemaining));
      }

      // Update osae-komi display or use frozen display if stopped.
      if (fight.osaeKomiSide && fight.osaeKomiStartedAtUtc) {
        const side = fight.osaeKomiSide === 'White' ? 'white' : 'blue';
        const cap = this.getOsaeKomiCap(fight, side);
        const holdSeconds = Math.min(cap, Math.ceil((Date.now() - new Date(fight.osaeKomiStartedAtUtc).getTime()) / 1000));
        this.osaeKomiSeconds.set(holdSeconds);
        this.osaeKomiCapSeconds.set(cap);
        this.osaeKomiSide.set(side);
        this.frozenOsaeKomiDisplay = null; // Active hold, no frozen display.
      } else {
        // No active osae-komi: clear signals (display will use frozen fallback).
        // This allows start buttons to be re-enabled after stopping osae-komi.
        this.osaeKomiSeconds.set(null);
        this.osaeKomiCapSeconds.set(null);
        this.osaeKomiSide.set(null);
      }

      // Auto-pause condition: only when time expired AND no osae-komi is active.
      const timeExpired = (fight.isGoldenScore && this.timerSeconds() === 0) || (!fight.isGoldenScore && regularRemaining <= 0);
      const shouldAutoPause = timeExpired && fight.status === 'InProgress' && fight.osaeKomiSide === null;

      if (shouldAutoPause && this.timerHandle !== null) {
        this.stopTimer();
        this.tryAutoPauseAtLimit(fight);
      }
    };

    tick();
    this.timerHandle = setInterval(tick, 250);
  }

  private stopTimer(): void {
    if (this.timerHandle !== null) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
    }
  }

  private tryAutoPauseAtLimit(fight: Fight): void {
    // Do not auto-pause if osae-komi is active; let the hold continue.
    if (fight.osaeKomiSide !== null) {
      return;
    }

    if (!this.canOperate() || fight.status !== 'InProgress') {
      return;
    }

    if (this.autoPauseInFlightFightId === fight.id) {
      return;
    }

    const tid = this.context.tournamentId();
    if (!tid) {
      return;
    }

    this.autoPauseInFlightFightId = fight.id;
    this.api.pauseFight(tid, fight.id, this.operatorName()).subscribe({
      next: () => {
        this.autoPauseInFlightFightId = null;
        this.refreshQueue();
      },
      error: () => {
        this.autoPauseInFlightFightId = null;
        this.errorMessage.set('Kampf konnte nicht automatisch gestoppt werden.');
      },
    });
  }

  protected formatTimer(seconds: number | null): string {
    if (seconds === null) return '--:--';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  protected athleteName(id: string | null): string {
    if (!id) return '?';
    const a = this.athletes().get(id);
    return a ? `${a.lastName}, ${a.firstName}` : id.substring(0, 8);
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
    if (!this.canOperate()) return;
    const tid = this.context.tournamentId();
    if (!tid) return;
    this.api.confirmResult(tid, fight.id, { winnerId }, this.operatorName()).subscribe({
      next: () => { this.queue.set(null); this.refreshQueue(); },
      error: () => this.errorMessage.set('Ergebnis konnte nicht bestätigt werden.'),
    });
  }

  protected saveOperatorName(name: string): void {
    this.operatorName.set(name);
    try { localStorage.setItem(OPERATOR_NAME_KEY, name); } catch { /* ignore */ }
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

  protected scoreImpact(scoreType: ScoreType): number {
    switch (scoreType) {
      case 'Ippon': return 10;
      case 'WazaAri': return 7;
      case 'Yuko': return 1;
      case 'Shido': return 0;
    }
  }

  protected hasWazaAri(fight: Fight, side: FightSide): boolean {
    return side === 'white' ? fight.whiteWazaAriCount > 0 : fight.blueWazaAriCount > 0;
  }

  protected holdTimerLabel(): string {
    const seconds = this.osaeKomiSeconds() ?? this.frozenOsaeKomiDisplay?.seconds ?? null;
    const cap = this.osaeKomiCapSeconds() ?? this.frozenOsaeKomiDisplay?.cap ?? 25;
    if (seconds === null) return '--s';
    return `${seconds}s / ${cap}s`;
  }

  protected fightTimerLabel(): string {
    return this.formatTimer(this.timerSeconds());
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
    return this.hasWazaAri(fight, side) ? 20 : 25;
  }

  private restoreOperatorName(): string {
    try { return localStorage.getItem(OPERATOR_NAME_KEY) ?? 'Operator'; } catch { return 'Operator'; }
  }
}
