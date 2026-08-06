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
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { combineLatest, Subscription } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { SideThemeService } from '../../core/side-theme.service';
import { TimeService } from '../../core/time.service';
import { CategoryFightsUpdatedEvent, TournamentHubService } from '../../core/tournament-hub.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { Athlete, Category, Club, Fight, FightSide, Tatami, TatamiQueue, Tournament } from '../../core/models';

interface TatamiDisplay {
  tatami: Tatami;
  current: Fight | null;
  nextFights: Fight[];
  /** Distinct categories with open fights on this tatami, ordered by operational relevance. */
  activeCategoryIds: string[];
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
  protected readonly sideTheme = inject(SideThemeService);
  private readonly hub = inject(TournamentHubService);
  private readonly time = inject(TimeService);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly tournamentId = signal<string | null>(null);
  protected readonly tatamiModeTatamiId = signal<string | null>(null);
  protected readonly tournament = signal<Tournament | null>(null);
  protected readonly tournamentName = signal<string>('');
  protected readonly displays = signal<TatamiDisplay[]>([]);
  protected readonly athletes = signal<Map<string, Athlete>>(new Map());
  protected readonly clubs = signal<Map<string, Club>>(new Map());
  protected readonly categories = signal<Map<string, Category>>(new Map());
  protected readonly nowEpochMs = signal<number>(Date.now());
  /** Inline SVG QR code for the public guest-share link, or null when the share is off. */
  protected readonly guestShareQr = signal<SafeHtml | null>(null);
  protected readonly hubConnected = computed(() => this.hub.connected());
  protected readonly isTatamiMode = computed(() => this.tatamiModeTatamiId() !== null);
  private readonly persistedOsaeKomiMap = new Map<string, { seconds: number; side: FightSide; clearOnResume: boolean }>();

  protected readonly tatamiDisplay = computed(() => {
    const tatamiId = this.tatamiModeTatamiId();
    if (!tatamiId) {
      return null;
    }

    return this.displays().find((display) => display.tatami.id === tatamiId) ?? null;
  });

  /** Active tatamis only, used for the "active categories per tatami" overview block. */
  protected readonly activeDisplays = computed(() =>
    this.displays().filter((display) => display.tatami.isActive));

  private fightSub?: Subscription;
  private serverTimeSub?: Subscription;
  private reconnectSub?: Subscription;
  private categoryFightsSub?: Subscription;
  private querySub?: Subscription;
  private timerHandle: ReturnType<typeof setInterval> | null = null;
  private lastTatamiQueueRefreshAt = 0;
  private lastGuestShareRefreshAt = 0;
  private lastClockResyncCheckAtMs = 0;
  private tatamiQueueRefreshInFlight = false;

  ngOnInit(): void {
    void this.time.synchronize(5);

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

    this.fightSub = this.hub.fightUpdated$.subscribe((fight) => {
      this.updateDisplayedFight(fight);

      const tid = this.tournamentId();
      if (tid) {
        this.refreshQueues(tid);
      }
    });

    this.categoryFightsSub = this.hub.categoryFightsUpdated$.subscribe((evt) => {
      this.handleCategoryFightsUpdated(evt);
    });

    this.serverTimeSub = this.hub.serverTimeSync$.subscribe((serverNowUtc) => {
      this.time.ingestServerNowUtc(serverNowUtc);
    });

    this.reconnectSub = this.hub.reconnected$.subscribe(() => {
      void this.time.synchronize(5);
    });

    this.timerHandle = setInterval(() => {
      const localNowMs = Date.now();
      if (localNowMs - this.lastClockResyncCheckAtMs >= 10_000) {
        this.lastClockResyncCheckAtMs = localNowMs;
        void this.time.synchronizeIfStale();
      }

      const now = this.time.nowMs();
      this.nowEpochMs.set(now);

      const tid = this.tournamentId();
      if (tid && this.isTatamiMode() && now - this.lastTatamiQueueRefreshAt >= 2_000) {
        this.lastTatamiQueueRefreshAt = now;
        this.refreshCurrentTatamiQueue(tid);
      }

      if (tid && !this.isTatamiMode() && now - this.lastGuestShareRefreshAt >= 15_000) {
        this.lastGuestShareRefreshAt = now;
        this.refreshGuestShare(tid);
      }
    }, 100);
  }

  ngOnDestroy(): void {
    this.querySub?.unsubscribe();
    this.fightSub?.unsubscribe();
    this.serverTimeSub?.unsubscribe();
    this.reconnectSub?.unsubscribe();
    this.categoryFightsSub?.unsubscribe();
    if (this.timerHandle !== null) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
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
    this.loadCategories(tid);
    this.refreshQueues(tid);
    this.refreshGuestShare(tid);
  }

  /**
   * Loads the public guest-share QR code for the tournament overview. The QR box is only
   * shown when the share is currently active; in single-tatami mode it is never shown.
   */
  private refreshGuestShare(tid: string): void {
    if (this.isTatamiMode()) {
      this.guestShareQr.set(null);
      return;
    }

    this.api.getGuestShare(tid).subscribe({
      next: (state) => {
        if (!state.isActive) {
          this.guestShareQr.set(null);
          return;
        }

        this.api.getGuestShareQr(tid).subscribe({
          next: (svg) => this.guestShareQr.set(this.sanitizer.bypassSecurityTrustHtml(svg)),
          error: () => this.guestShareQr.set(null),
        });
      },
      error: () => this.guestShareQr.set(null),
    });
  }

  @HostListener('document:visibilitychange')
  protected onVisibilityChange(): void {
    if (document.visibilityState === 'visible') {
      void this.time.synchronizeIfStale();
    }
  }

  private handleCategoryFightsUpdated(evt: CategoryFightsUpdatedEvent): void {
    const tid = this.tournamentId();
    if (!tid || evt.tournamentId !== tid) {
      return;
    }

    // A draw generation can change category names and tatami assignments.
    this.loadCategories(tid);
    this.refreshQueues(tid);
  }

  private loadCategories(tid: string): void {
    this.api.getCategories(tid).subscribe({
      next: categories => {
        this.categories.set(new Map(categories.map(category => [category.id, category])));
      },
    });
  }

  /**
   * Distinct categories with open fights assigned to the tatami, ordered by the queue's
   * operational relevance (in-progress first, then upcoming). The queue already excludes
   * completed fights, so every listed category still has at least one open fight.
   */
  private buildActiveCategoryIds(queue: TatamiQueue): string[] {
    const seen = new Set<string>();
    const ordered: string[] = [];
    for (const fight of queue.upcoming) {
      if (!seen.has(fight.categoryId)) {
        seen.add(fight.categoryId);
        ordered.push(fight.categoryId);
      }
    }
    return ordered;
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
            if (q.current) {
              this.syncOsaeKomiSnapshot(q.current);
            }

            const nextFights = this.buildNextFights(q);
            this.displays.set([{ tatami: selectedTatami, current: q.current, nextFights, activeCategoryIds: this.buildActiveCategoryIds(q) }]);
          },
          error: () => {
            this.displays.set([{ tatami: selectedTatami, current: null, nextFights: [], activeCategoryIds: [] }]);
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
            if (q.current) {
              this.syncOsaeKomiSnapshot(q.current);
            }

            const nextFights = this.buildNextFights(q);
            updates.push({ tatami, current: q.current, nextFights, activeCategoryIds: this.buildActiveCategoryIds(q) });
            pending--;
            if (pending === 0) {
              this.displays.set(updates.sort((a, b) => a.tatami.displayOrder - b.tatami.displayOrder || a.tatami.name.localeCompare(b.tatami.name)));
            }
          },
          error: () => {
            updates.push({ tatami, current: null, nextFights: [], activeCategoryIds: [] });
            pending--;
            if (pending === 0) {
              this.displays.set(updates.sort((a, b) => a.tatami.displayOrder - b.tatami.displayOrder || a.tatami.name.localeCompare(b.tatami.name)));
            }
          },
        });
      });
    });
  }

  private updateDisplayedFight(fight: Fight): void {
    this.syncOsaeKomiSnapshot(fight);

    this.displays.update(displays => displays.map(display => ({
      ...display,
      current: display.current?.id === fight.id ? fight : display.current,
      nextFights: display.nextFights.map(nextFight => nextFight.id === fight.id ? fight : nextFight),
    })));
  }

  private refreshCurrentTatamiQueue(tournamentId: string): void {
    const tatamiId = this.tatamiModeTatamiId();
    if (!tatamiId || this.tatamiQueueRefreshInFlight) {
      return;
    }

    this.tatamiQueueRefreshInFlight = true;
    this.api.getTatamiQueue(tournamentId, tatamiId).subscribe({
      next: queue => {
        if (queue.current) {
          this.updateDisplayedFight(queue.current);
        }

        const nextFights = this.buildNextFights(queue);

        this.displays.update(displays => displays.map(display =>
          display.tatami.id === tatamiId
            ? { ...display, current: queue.current, nextFights, activeCategoryIds: this.buildActiveCategoryIds(queue) }
            : display));
        this.tatamiQueueRefreshInFlight = false;
      },
      error: () => {
        this.tatamiQueueRefreshInFlight = false;
      },
    });
  }

  private buildNextFights(queue: TatamiQueue): Fight[] {
    const currentId = queue.current?.id;
    return queue.upcoming
      .filter((fight) => fight.id !== currentId)
      .slice(0, 3);
  }

  protected hasIppon(fight: Fight, side: FightSide): boolean {
    return this.scoreCount(fight, side, 'ippon') > 0;
  }

  protected isOsaeKomiRunning(fight: Fight): boolean {
    return fight.osaeKomiSide !== null && fight.osaeKomiStartedAtUtc !== null;
  }

  protected hasPersistedOsaeKomi(fight: Fight): boolean {
    return this.persistedOsaeKomiMap.has(fight.id);
  }

  protected osaeKomiSideLabel(fight: Fight): FightSide | null {
    if (fight.osaeKomiSide) {
      return fight.osaeKomiSide === 'White' ? 'white' : 'blue';
    }

    return this.persistedOsaeKomiMap.get(fight.id)?.side ?? null;
  }

  protected osaeKomiSecondsLabel(fight: Fight): string {
    if (fight.osaeKomiSide && fight.osaeKomiStartedAtUtc) {
      const side = this.osaeKomiSideLabel(fight);
      if (!side) {
        return '--';
      }

      const capSeconds = this.getOsaeKomiCapForFight(fight, side);
      const elapsedExactSeconds = Math.max(0, Math.min(capSeconds, (this.nowEpochMs() - new Date(fight.osaeKomiStartedAtUtc).getTime()) / 1000));
      const runningSeconds = Math.min(capSeconds, Math.max(0, Math.ceil(elapsedExactSeconds)));
      const remainingToCap = Math.max(0, capSeconds - elapsedExactSeconds);
      const showTenths = remainingToCap <= 10;
      return showTenths ? `${elapsedExactSeconds.toFixed(1)}s` : `${runningSeconds}s`;
    }

    const persisted = this.persistedOsaeKomiMap.get(fight.id);
    if (persisted) {
      return `${persisted.seconds}s`;
    }

    return '--';
  }

  protected osaeKomiCapSecondsLabel(fight: Fight): string {
    const side = this.osaeKomiSideLabel(fight);
    if (!side) {
      return '--';
    }

    return `${this.getOsaeKomiCapForFight(fight, side)}s`;
  }

  private hasWazaAri(fight: Fight, side: FightSide): boolean {
    return side === 'white' ? fight.whiteWazaAriCount > 0 : fight.blueWazaAriCount > 0;
  }

  private getOsaeKomiCapForFight(fight: Fight, side: FightSide): number {
    const t = this.tournament();
    if (this.hasWazaAri(fight, side)) {
      return t?.osaeKomiWazaAriSeconds ?? 10;
    }
    return t?.osaeKomiIpponSeconds ?? 20;
  }

  // Osae-komi display state matrix for the tatami screen:
  // - Start: active backend fields win and refresh the persisted snapshot.
  // - Stop: keep the last snapshot visible until a new osae-komi starts.
  // - Pause: keep the last snapshot visible while the fight remains paused and mark it for resume clearing.
  // - Resume: clear a snapshot that was marked during pause when the fight returns to normal InProgress without active osae-komi.
  // - Pending/Completed: always clear the snapshot because the fight is no longer active.
  private syncOsaeKomiSnapshot(fight: Fight): void {
    if (this.isOsaeKomiRunning(fight)) {
      const side = fight.osaeKomiSide === 'White' ? 'white' : 'blue';
      const cap = this.getOsaeKomiCapForFight(fight, side);
      const startedAtMs = new Date(fight.osaeKomiStartedAtUtc!).getTime();
      const seconds = Math.min(cap, Math.max(0, Math.ceil((this.nowEpochMs() - startedAtMs) / 1000)));
      this.persistedOsaeKomiMap.set(fight.id, { seconds, side, clearOnResume: false });
    } else if (fight.status === 'Pending' || fight.status === 'Completed') {
      this.persistedOsaeKomiMap.delete(fight.id);
    } else {
      const persisted = this.persistedOsaeKomiMap.get(fight.id);
      if (!persisted) {
        return;
      }

      if (fight.status === 'Paused') {
        this.persistedOsaeKomiMap.set(fight.id, { ...persisted, clearOnResume: true });
        return;
      }

      if (fight.status === 'InProgress' && persisted.clearOnResume) {
        this.persistedOsaeKomiMap.delete(fight.id);
      }
    }
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

    if (!fight.startedAtUtc) {
      // Fight not yet started: show configured match duration.
      const cat = this.categories().get(fight.categoryId);
      const matchDuration = cat?.matchDurationSeconds ?? 300;
      return this.formatWholeSeconds(matchDuration);
    }

    const cat = this.categories().get(fight.categoryId);
    const matchDuration = cat?.matchDurationSeconds ?? 300;
    const goldenScoreDuration = cat?.goldenScoreDurationSeconds ?? 180;

    const timerReference = fight.status === 'Paused' && fight.pausedAtUtc
      ? new Date(fight.pausedAtUtc).getTime()
      : this.nowEpochMs();
    const elapsedSeconds = (timerReference - new Date(fight.startedAtUtc).getTime()) / 1000;

    // Use fight.isGoldenScore from server (reload-safe, score-aware).
    if (fight.isGoldenScore) {
      const gsElapsed = elapsedSeconds - matchDuration;
      const gsRemaining = Math.max(0, goldenScoreDuration - gsElapsed);
      const showTenths = fight.status === 'InProgress' && gsRemaining <= 10;
      return showTenths
        ? this.formatTenthsCountdown(gsRemaining)
        : this.formatWholeSeconds(gsRemaining);
    }

    const remainingSeconds = Math.max(0, matchDuration - elapsedSeconds);
    const showTenths = fight.status === 'InProgress' && remainingSeconds <= 10;
    return showTenths
      ? this.formatTenthsCountdown(remainingSeconds)
      : this.formatWholeSeconds(remainingSeconds);
  }

  /** Splits the timer label around its minutes:seconds colon so the colon can
   *  carry the softened blink decoration while the digits stay static. */
  protected timerColonParts(fight: Fight): { minutes: string; seconds: string } {
    const label = this.timerForFight(fight);
    const idx = label.indexOf(':');
    return idx < 0
      ? { minutes: label, seconds: '' }
      : { minutes: label.slice(0, idx), seconds: label.slice(idx + 1) };
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

  protected athleteName(id: string | null): string {
    if (!id) return '?';
    const a = this.athletes().get(id);
    return a ? `${a.lastName}, ${a.firstName}` : id.substring(0, 8);
  }

}
