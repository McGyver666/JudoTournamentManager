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
  /** Tracks fights that already transitioned from regular to golden score (awaiting manual resume). */
  private readonly goldenScoreTransitionedFightIds = new Set<string>();

  protected readonly scoreTypes: ScoreType[] = ['Ippon', 'WazaAri', 'Yuko', 'Shido'];

  protected readonly hubConnected = computed(() => this.hub.connected());
  protected readonly currentFight = computed(() => this.queue()?.current ?? null);

  private fightSub?: Subscription;

  ngOnInit(): void {
    const tid = this.context.tournamentId();
    if (!tid) return;

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
    if (!fight || !fight.startedAtUtc) {
      this.timerSeconds.set(null);
      this.timerIsGoldenScore.set(false);
      this.osaeKomiSeconds.set(null);
      this.osaeKomiCapSeconds.set(null);
      this.osaeKomiSide.set(null);
      return;
    }

    const categoryId = fight.categoryId;
    const cat = this.categories().find(c => c.id === categoryId);
    const duration = cat?.matchDurationSeconds ?? 300;
    const goldenScoreEnabled = cat?.goldenScoreEnabled ?? false;
    const goldenScoreDuration = cat?.goldenScoreDurationSeconds ?? 180;

    const tick = () => {
      const timerReference = fight.status === 'Paused' && fight.pausedAtUtc ? fight.pausedAtUtc : new Date();
      const elapsed = (new Date(timerReference).getTime() - new Date(fight.startedAtUtc!).getTime()) / 1000;
      const regularRemaining = Math.max(0, duration - elapsed);
      const isTransitioned = this.goldenScoreTransitionedFightIds.has(fight.id);

      // First regular expiry with golden enabled while InProgress: stop and wait for manual resume.
      if (!isTransitioned && goldenScoreEnabled && fight.status === 'InProgress' && regularRemaining <= 0) {
        this.goldenScoreTransitionedFightIds.add(fight.id);
        this.timerIsGoldenScore.set(true);
        this.timerSeconds.set(goldenScoreDuration);
        this.stopTimer();
        this.tryAutoPauseAtLimit(fight);
        return;
      }

      if (isTransitioned) {
        // Golden score phase: frozen while paused, counting down while in progress.
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

      if (fight.osaeKomiSide && fight.osaeKomiStartedAtUtc) {
        const side = fight.osaeKomiSide === 'White' ? 'white' : 'blue';
        const cap = this.getOsaeKomiCap(fight, side);
        const holdSeconds = Math.min(cap, Math.ceil((Date.now() - new Date(fight.osaeKomiStartedAtUtc).getTime()) / 1000));
        this.osaeKomiSeconds.set(holdSeconds);
        this.osaeKomiCapSeconds.set(cap);
        this.osaeKomiSide.set(side);
      } else {
        this.osaeKomiSeconds.set(null);
        this.osaeKomiCapSeconds.set(null);
        this.osaeKomiSide.set(null);
      }

      // For non-golden categories: stop at regular expiry.
      // For golden categories after manual resume: stop when golden countdown reaches zero.
      const reachedRegularLimit = !goldenScoreEnabled && regularRemaining <= 0;
      const reachedGoldenLimit = isTransitioned && fight.status === 'InProgress' && this.timerSeconds() === 0;
      if ((reachedRegularLimit || reachedGoldenLimit) && this.timerHandle !== null) {
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
      next: () => { this.goldenScoreTransitionedFightIds.delete(fight.id); this.queue.set(null); this.refreshQueue(); },
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
    const seconds = this.osaeKomiSeconds();
    if (seconds === null) return '--s';
    return `${seconds}s / ${this.osaeKomiCapSeconds() ?? 25}s`;
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

  protected activeHoldSideLabel(): string | null {
    const side = this.osaeKomiSide();
    if (!side) return null;
    return this.sideLabel(side);
  }

  protected sideLabel(side: FightSide): string {
    return side === 'white' ? 'Weiß' : 'Blau';
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
