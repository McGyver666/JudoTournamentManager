import type { Mock } from 'vitest';
import { signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { Athlete, Category, Club, Fight, Tatami, TatamiQueue, Tournament } from '../../core/models';
import { I18nService } from '../../core/i18n.service';
import { SideThemeService } from '../../core/side-theme.service';
import { TimeService } from '../../core/time.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { MatchComponent } from './match.component';

describe('MatchComponent', () => {
  let fightUpdates: Subject<Fight>;
  let categoryFightsUpdates: Subject<{
    tournamentId: string;
    categoryId: string;
  }>;
  let getTatamiQueueSpy: Mock;
  let getAthletesSpy: Mock;
  let startFightSpy: Mock;
  let pauseFightSpy: Mock;
  let resumeFightSpy: Mock;
  let startOsaeKomiSpy: Mock;
  let stopOsaeKomiSpy: Mock;
  let tournamentSignal: WritableSignal<Tournament>;

  function createTournament(): Tournament {
    return {
      id: 'tournament-1',
      name: 'Testturnier',
      date: '2026-07-27',
      venue: 'Halle 1',
      organizer: 'Club',
      competitionMode: 'Individual',
      teamMatchdayProfile: null,
      accentSideColor: 'Blue',
      osaeKomiIpponSeconds: 20,
      osaeKomiWazaAriSeconds: 10,
      osaeKomiYukoSeconds: 5,
      osaeKomiYukoEnabled: true,
      minimumRestBetweenFightsSeconds: 180,
      twoThirdPlacesInRoundRobin: false,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
    };
  }

  function createTatami(): Tatami {
    return {
      id: 'tatami-1',
      tournamentId: 'tournament-1',
      name: 'Matte 1',
      displayOrder: 1,
      isActive: true,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
    };
  }

  function createFight(overrides: Partial<Fight> = {}): Fight {
    return {
      id: 'fight-1',
      tournamentId: 'tournament-1',
      categoryId: 'category-1',
      bracketType: 'Main',
      round: 1,
      fightNumber: 1,
      poolNumber: null,
      whiteSourceFightId: null,
      whiteSourceOutcome: null,
      blueSourceFightId: null,
      blueSourceOutcome: null,
      whiteAthleteId: 'athlete-white',
      blueAthleteId: 'athlete-blue',
      winnerId: null,
      isBye: false,
      status: 'Completed',
      tatamiId: 'tatami-1',
      queueOrder: 0,
      whiteScore: 1,
      blueScore: 0,
      whitePenalties: 0,
      bluePenalties: 0,
      whiteIpponCount: 1,
      whiteWazaAriCount: 0,
      whiteYukoCount: 0,
      blueIpponCount: 0,
      blueWazaAriCount: 0,
      blueYukoCount: 0,
      pausedAtUtc: null,
      osaeKomiSide: null,
      osaeKomiStartedAtUtc: null,
      osaeKomiPausedAtUtc: null,
      osaeKomiElapsedMilliseconds: 0,
      startedAtUtc: new Date(Date.now() - 60000).toISOString(),
      completedAtUtc: new Date().toISOString(),
      isGoldenScore: false,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
      ...overrides,
    };
  }

  beforeEach(() => {
    fightUpdates = new Subject<Fight>();
    categoryFightsUpdates = new Subject<{
      tournamentId: string;
      categoryId: string;
    }>();

    const tournament = createTournament();
    tournamentSignal = signal(tournament);

    const apiMock: Partial<ApiService> = {
      getTournament: vi.fn().mockName('getTournament').mockReturnValue(of(tournament)),
      getAthletes: vi.fn().mockName('getAthletes').mockReturnValue(of([] as Athlete[])),
      getClubs: vi.fn().mockName('getClubs').mockReturnValue(of([] as Club[])),
      getCategories: vi.fn().mockName('getCategories').mockReturnValue(of([{
        id: 'category-1',
        tournamentId: 'tournament-1',
        name: 'U18 -66',
        ageGroup: 'U18',
        gender: 'Male',
        weightClassKg: 66,
        minBirthYear: null,
        maxBirthYear: null,
        rulesetNotes: null,
        matchDurationSeconds: 240,
        goldenScoreEnabled: true,
        goldenScoreDurationSeconds: 180,
        drawFormat: 'SingleElimination',
        isLocked: true,
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
      } as Category])),
      getTatamis: vi.fn().mockName('getTatamis').mockReturnValue(of([createTatami()])),
      getTatamiQueue: vi.fn().mockName('getTatamiQueue').mockReturnValue(of({
        current: null,
        next: null,
        onDeck: null,
        upcoming: [],
      } as TatamiQueue)),
      startFight: vi.fn().mockName('startFight').mockReturnValue(of(undefined)),
      pauseFight: vi.fn().mockName('pauseFight').mockReturnValue(of(undefined)),
      resumeFight: vi.fn().mockName('resumeFight').mockReturnValue(of(undefined)),
      startOsaeKomi: vi.fn().mockName('startOsaeKomi').mockReturnValue(of(undefined)),
      stopOsaeKomi: vi.fn().mockName('stopOsaeKomi').mockReturnValue(of(undefined)),
    };

    getTatamiQueueSpy = apiMock.getTatamiQueue as Mock;
    getAthletesSpy = apiMock.getAthletes as Mock;
    startFightSpy = apiMock.startFight as Mock;
    pauseFightSpy = apiMock.pauseFight as Mock;
    resumeFightSpy = apiMock.resumeFight as Mock;
    startOsaeKomiSpy = apiMock.startOsaeKomi as Mock;
    stopOsaeKomiSpy = apiMock.stopOsaeKomi as Mock;

    TestBed.configureTestingModule({
      providers: [
        { provide: ApiService, useValue: apiMock },
        { provide: AuthStateService, useValue: { canOperate: signal(true) } },
        {
          provide: TournamentContextService,
          useValue: {
            tournamentId: signal('tournament-1'),
            tournament: tournamentSignal,
            refreshIfActive: () => undefined,
          },
        },
        {
          provide: TournamentHubService,
          useValue: {
            connected: signal(true),
            connect: () => Promise.resolve(),
            fightUpdated$: fightUpdates.asObservable(),
            categoryFightsUpdated$: categoryFightsUpdates.asObservable(),
            serverTimeSync$: new Subject<string>().asObservable(),
            reconnected$: new Subject<void>().asObservable(),
          },
        },
        {
          provide: SideThemeService,
          useValue: {
            applyTheme: () => undefined,
            accentSideLabelKey: () => 'match.blueSide',
            confirmWinnerLabelKey: () => 'match.confirmBlueWins',
            startOsaeLabelKey: () => 'match.startOsaeBlue',
          },
        },
        { provide: TimeService, useValue: { synchronize: () => Promise.resolve(), synchronizeIfStale: () => Promise.resolve(), ingestServerNowUtc: () => undefined, nowMs: () => Date.now() } },
        { provide: I18nService, useValue: { translate: (key: string) => key } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({})), queryParamMap: of(convertToParamMap({ tatamiId: 'tatami-1' })) } },
        { provide: Router, useValue: { navigate: vi.fn().mockName('navigate').mockResolvedValue(true) } },
      ],
    });
  });

  it('starts a pending fight with the space bar and refreshes the queue', () => {
    getTatamiQueueSpy.mockReturnValue(of({
      current: createFight({ status: 'Pending', startedAtUtc: null, completedAtUtc: null }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();
    getTatamiQueueSpy.mockClear();

    const event = new KeyboardEvent('keydown', { key: ' ', code: 'Space', cancelable: true });
    document.dispatchEvent(event);

    expect(startFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', expect.any(String));
    expect(getTatamiQueueSpy).toHaveBeenCalledTimes(1);
    expect(event.defaultPrevented).toBe(true);

    fixture.destroy();
  });

  it('pauses and resumes the current fight with the space bar', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    const component = fixture.componentInstance as any;

    getTatamiQueueSpy.mockReturnValue(of({
      current: createFight({ status: 'InProgress' }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(pauseFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', expect.any(String));

    component.queue.set({
      current: createFight({ status: 'Paused' }),
      next: null,
      onDeck: null,
      upcoming: [],
    });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(resumeFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', expect.any(String));
    expect(startFightSpy).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('starts and stops Osae-komi with S, F, and D without touching the fight clock', () => {
    tournamentSignal.update((tournament) => ({ ...tournament, accentSideColor: 'Red' }));
    const fixture = TestBed.createComponent(MatchComponent);
    const component = fixture.componentInstance as any;
    const fight = createFight({ status: 'InProgress' });
    getTatamiQueueSpy.mockReturnValue(of({ current: fight, next: null, onDeck: null, upcoming: [] } as TatamiQueue));
    fixture.detectChanges();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'f' }));
    component.osaeKomiSide.set('white');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));

    expect(startOsaeKomiSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', { side: 'white' }, expect.any(String));
    expect(startOsaeKomiSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', { side: 'blue' }, expect.any(String));
    expect(stopOsaeKomiSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', expect.any(String));
    expect(pauseFightSpy).not.toHaveBeenCalled();
    expect(resumeFightSpy).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('ignores repeated, modified, invalid, unauthorized, and focused-input shortcuts', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    const auth = TestBed.inject(AuthStateService) as AuthStateService & {
      canOperate: WritableSignal<boolean>;
    };
    const component = fixture.componentInstance as any;
    const fight = createFight({ status: 'InProgress' });
    getTatamiQueueSpy.mockReturnValue(of({ current: fight, next: null, onDeck: null, upcoming: [] } as TatamiQueue));
    fixture.detectChanges();

    component.osaeKomiSide.set(null);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 's', repeat: true }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', ctrlKey: true }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));
    auth.canOperate.set(false);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));
    auth.canOperate.set(true);

    const input = document.createElement('input');
    document.body.appendChild(input);
    input.focus();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(startFightSpy).not.toHaveBeenCalled();
    expect(pauseFightSpy).not.toHaveBeenCalled();
    expect(startOsaeKomiSpy).not.toHaveBeenCalled();
    expect(stopOsaeKomiSpy).not.toHaveBeenCalled();

    input.remove();
    fixture.destroy();
  });

  it('does not handle shortcuts for a completed or missing current fight', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    const component = fixture.componentInstance as any;
    fixture.detectChanges();

    component.queue.set({
      current: createFight({ status: 'Completed' }),
      next: null,
      onDeck: null,
      upcoming: [],
    });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'f' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));
    component.queue.set({ current: null, next: null, onDeck: null, upcoming: [] });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(startFightSpy).not.toHaveBeenCalled();
    expect(startOsaeKomiSpy).not.toHaveBeenCalled();
    expect(stopOsaeKomiSpy).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('ignores shortcuts without a selected tatami, tournament, or outside a modal dialog', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    const component = fixture.componentInstance as any;
    const fight = createFight({ status: 'Pending', startedAtUtc: null, completedAtUtc: null });
    getTatamiQueueSpy.mockReturnValue(of({ current: fight, next: null, onDeck: null, upcoming: [] } as TatamiQueue));
    fixture.detectChanges();

    component.selectedTatamiId.set(null);
    const noTatamiEvent = new KeyboardEvent('keydown', { key: ' ', cancelable: true });
    document.dispatchEvent(noTatamiEvent);

    component.selectedTatamiId.set('tatami-1');
    component.context.tournamentId.set(null);
    const noTournamentEvent = new KeyboardEvent('keydown', { key: ' ', cancelable: true });
    document.dispatchEvent(noTournamentEvent);

    component.context.tournamentId.set('tournament-1');
    component.winnerConfirmation.set({ fight, winnerId: fight.whiteAthleteId, nextFight: null });
    const dialogEvent = new KeyboardEvent('keydown', { key: ' ', cancelable: true });
    document.dispatchEvent(dialogEvent);

    expect(startFightSpy).not.toHaveBeenCalled();
    expect(noTatamiEvent.defaultPrevented).toBe(false);
    expect(noTournamentEvent.defaultPrevented).toBe(false);
    expect(dialogEvent.defaultPrevented).toBe(false);
    fixture.destroy();
  });

  it('uses the existing error path when a keyboard action fails', () => {
    getTatamiQueueSpy.mockReturnValue(of({
      current: createFight({ status: 'Pending', startedAtUtc: null, completedAtUtc: null }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));
    startFightSpy.mockReturnValue(throwError(() => new Error('start failed')));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();
    const event = new KeyboardEvent('keydown', { key: ' ', cancelable: true });
    document.dispatchEvent(event);

    expect(startFightSpy).toHaveBeenCalled();
    expect((fixture.componentInstance as any).errorMessage()).toBe('Kampf konnte nicht gestartet werden.');
    expect(event.defaultPrevented).toBe(true);
    fixture.destroy();
  });

  it('uses the shared keyboard lifecycle for TeamMatchday fights', () => {
    tournamentSignal.update((tournament) => ({ ...tournament, competitionMode: 'TeamMatchday' }));
    getTatamiQueueSpy.mockReturnValue(of({
      current: createFight({ status: 'InProgress' }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(pauseFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', expect.any(String));
    fixture.destroy();
  });

  it('replaces the shido bubbles with a Hansoku-make badge after the third shido', () => {
    getTatamiQueueSpy.mockReturnValue(of({
      current: createFight({ status: 'InProgress', whitePenalties: 3, blueIpponCount: 1, whiteIpponCount: 0 }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();

    const white = fixture.nativeElement.querySelector('.athlete-card--white') as HTMLElement;
    const blue = fixture.nativeElement.querySelector('.athlete-card--blue') as HTMLElement;
    const badges = white.querySelectorAll('.hansoku-make-badge');
    expect(badges.length).toBeGreaterThan(0);
    badges.forEach((badge) => expect(badge.textContent).toContain('match.hansokuMake'));
    expect(white.querySelectorAll('.shido-bubble').length).toBe(0);
    expect(blue.querySelectorAll('.hansoku-make-badge').length).toBe(0);
    expect(blue.querySelectorAll('.shido-bubble').length).toBeGreaterThan(0);

    fixture.destroy();
  });

  it('renders the keyboard shortcut help in the match header', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector('.hotkey-help__trigger') as HTMLButtonElement;
    const popover = fixture.nativeElement.querySelector('#match-hotkeys-popover') as HTMLElement;
    const keys = Array.from(popover.querySelectorAll('kbd')).map((key) => key.textContent?.trim());

    expect(trigger).not.toBeNull();
    expect(trigger.getAttribute('aria-describedby')).toBe('match-hotkeys-popover');
    expect(popover.getAttribute('role')).toBe('tooltip');
    expect(keys).toEqual(['Space', 'S', 'F', 'D']);

    fixture.destroy();
  });

  it('refreshes queue and athlete metadata immediately after a completed fight update', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();

    getTatamiQueueSpy.mockClear();
    getAthletesSpy.mockClear();

    fightUpdates.next(createFight());

    expect(getTatamiQueueSpy).toHaveBeenCalledTimes(1);
    expect(getAthletesSpy).toHaveBeenCalledTimes(1);

    fixture.destroy();
  });

  it('shows and wires the Hiki-wake button for team-matchday fights', () => {
    tournamentSignal.update((tournament) => ({ ...tournament, competitionMode: 'TeamMatchday' }));
    getTatamiQueueSpy.mockReturnValue(of({
      current: createFight({ status: 'InProgress' }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('.confirm-row button');
    const drawButton = fixture.nativeElement.querySelector('.btn--draw') as HTMLButtonElement | null;
    expect(buttons.length).toBe(3);
    expect(drawButton).not.toBeNull();
    expect(drawButton?.textContent).toContain('match.confirmHikiwake');

    drawButton?.click();
    fixture.detectChanges();

    expect((fixture.componentInstance as any).winnerConfirmation().winnerId).toBeNull();
    fixture.destroy();
  });

  it('freezes the stopped Osae-Komi at the server mutation timestamp', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();

    const startedAt = new Date('2026-09-04T10:00:00.000Z');
    const activeHold = createFight({
      status: 'InProgress',
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: startedAt.toISOString(),
    });
    const stoppedFight = createFight({
      status: 'InProgress',
      updatedAtUtc: new Date(startedAt.getTime() + 5400).toISOString(),
    });

    (fixture.componentInstance as any).restartTimer(activeHold);
    (fixture.componentInstance as any).restartTimer(stoppedFight);

    expect((fixture.componentInstance as any).holdTimerLabel()).toBe('5.4s / 20s');

    fixture.destroy();
  });

  it('toggles Sono-mama and Yoshi while preserving the accumulated hold time', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance as any;
    const pausedHold = createFight({
      status: 'InProgress',
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: null,
      osaeKomiPausedAtUtc: new Date().toISOString(),
      osaeKomiElapsedMilliseconds: 3400,
    });

    component.restartTimer(pausedHold);

    expect(component.isOsaeKomiPaused(pausedHold)).toBe(true);
    expect(component.osaeKomiToggleLabelKey()).toBe('match.resumeOsae');
    expect(component.holdTimerLabel()).toBe('3.4s / 20s');

    const runningHold = createFight({
      status: 'InProgress',
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date().toISOString(),
      osaeKomiPausedAtUtc: null,
      osaeKomiElapsedMilliseconds: 3400,
    });

    component.restartTimer(runningHold);

    expect(component.isOsaeKomiPaused(runningHold)).toBe(false);
    expect(component.osaeKomiToggleLabelKey()).toBe('match.pauseOsae');

    fixture.destroy();
  });
});
