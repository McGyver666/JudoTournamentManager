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
  let categoryFightsUpdates: Subject<{ tournamentId: string; categoryId: string }>;
  let getTatamiQueueSpy: jasmine.Spy;
  let getAthletesSpy: jasmine.Spy;
  let startFightSpy: jasmine.Spy;
  let pauseFightSpy: jasmine.Spy;
  let resumeFightSpy: jasmine.Spy;
  let startOsaeKomiSpy: jasmine.Spy;
  let stopOsaeKomiSpy: jasmine.Spy;
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
      startedAtUtc: new Date(Date.now() - 60_000).toISOString(),
      completedAtUtc: new Date().toISOString(),
      isGoldenScore: false,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
      ...overrides,
    };
  }

  beforeEach(() => {
    fightUpdates = new Subject<Fight>();
    categoryFightsUpdates = new Subject<{ tournamentId: string; categoryId: string }>();

    const tournament = createTournament();
    tournamentSignal = signal(tournament);

    const apiMock: Partial<ApiService> = {
      getTournament: jasmine.createSpy('getTournament').and.returnValue(of(tournament)),
      getAthletes: jasmine.createSpy('getAthletes').and.returnValue(of([] as Athlete[])),
      getClubs: jasmine.createSpy('getClubs').and.returnValue(of([] as Club[])),
      getCategories: jasmine.createSpy('getCategories').and.returnValue(of([{ 
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
      getTatamis: jasmine.createSpy('getTatamis').and.returnValue(of([createTatami()])),
      getTatamiQueue: jasmine.createSpy('getTatamiQueue').and.returnValue(of({
        current: null,
        next: null,
        onDeck: null,
        upcoming: [],
      } as TatamiQueue)),
      startFight: jasmine.createSpy('startFight').and.returnValue(of(undefined)),
      pauseFight: jasmine.createSpy('pauseFight').and.returnValue(of(undefined)),
      resumeFight: jasmine.createSpy('resumeFight').and.returnValue(of(undefined)),
      startOsaeKomi: jasmine.createSpy('startOsaeKomi').and.returnValue(of(undefined)),
      stopOsaeKomi: jasmine.createSpy('stopOsaeKomi').and.returnValue(of(undefined)),
    };

    getTatamiQueueSpy = apiMock.getTatamiQueue as jasmine.Spy;
    getAthletesSpy = apiMock.getAthletes as jasmine.Spy;
    startFightSpy = apiMock.startFight as jasmine.Spy;
    pauseFightSpy = apiMock.pauseFight as jasmine.Spy;
    resumeFightSpy = apiMock.resumeFight as jasmine.Spy;
    startOsaeKomiSpy = apiMock.startOsaeKomi as jasmine.Spy;
    stopOsaeKomiSpy = apiMock.stopOsaeKomi as jasmine.Spy;

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
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true)) } },
      ],
    });
  });

  it('starts a pending fight with the space bar and refreshes the queue', () => {
    getTatamiQueueSpy.and.returnValue(of({
      current: createFight({ status: 'Pending', startedAtUtc: null, completedAtUtc: null }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();
    getTatamiQueueSpy.calls.reset();

    const event = new KeyboardEvent('keydown', { key: ' ', code: 'Space', cancelable: true });
    document.dispatchEvent(event);

    expect(startFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', jasmine.any(String));
    expect(getTatamiQueueSpy).toHaveBeenCalledTimes(1);
    expect(event.defaultPrevented).toBeTrue();

    fixture.destroy();
  });

  it('pauses and resumes the current fight with the space bar', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    const component = fixture.componentInstance as any;

    getTatamiQueueSpy.and.returnValue(of({
      current: createFight({ status: 'InProgress' }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(pauseFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', jasmine.any(String));

    component.queue.set({
      current: createFight({ status: 'Paused' }),
      next: null,
      onDeck: null,
      upcoming: [],
    });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(resumeFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', jasmine.any(String));
    expect(startFightSpy).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('starts and stops Osae-komi with S, F, and D without touching the fight clock', () => {
    tournamentSignal.update((tournament) => ({ ...tournament, accentSideColor: 'Red' }));
    const fixture = TestBed.createComponent(MatchComponent);
    const component = fixture.componentInstance as any;
    const fight = createFight({ status: 'InProgress' });
    getTatamiQueueSpy.and.returnValue(of({ current: fight, next: null, onDeck: null, upcoming: [] } as TatamiQueue));
    fixture.detectChanges();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'f' }));
    component.osaeKomiSide.set('white');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));

    expect(startOsaeKomiSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', { side: 'white' }, jasmine.any(String));
    expect(startOsaeKomiSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', { side: 'blue' }, jasmine.any(String));
    expect(stopOsaeKomiSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', jasmine.any(String));
    expect(pauseFightSpy).not.toHaveBeenCalled();
    expect(resumeFightSpy).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('ignores repeated, modified, invalid, unauthorized, and focused-input shortcuts', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    const auth = TestBed.inject(AuthStateService) as AuthStateService & { canOperate: WritableSignal<boolean> };
    const component = fixture.componentInstance as any;
    const fight = createFight({ status: 'InProgress' });
    getTatamiQueueSpy.and.returnValue(of({ current: fight, next: null, onDeck: null, upcoming: [] } as TatamiQueue));
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
    getTatamiQueueSpy.and.returnValue(of({ current: fight, next: null, onDeck: null, upcoming: [] } as TatamiQueue));
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
    expect(noTatamiEvent.defaultPrevented).toBeFalse();
    expect(noTournamentEvent.defaultPrevented).toBeFalse();
    expect(dialogEvent.defaultPrevented).toBeFalse();
    fixture.destroy();
  });

  it('uses the existing error path when a keyboard action fails', () => {
    getTatamiQueueSpy.and.returnValue(of({
      current: createFight({ status: 'Pending', startedAtUtc: null, completedAtUtc: null }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));
    startFightSpy.and.returnValue(throwError(() => new Error('start failed')));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();
    const event = new KeyboardEvent('keydown', { key: ' ', cancelable: true });
    document.dispatchEvent(event);

    expect(startFightSpy).toHaveBeenCalled();
    expect((fixture.componentInstance as any).errorMessage()).toBe('Kampf konnte nicht gestartet werden.');
    expect(event.defaultPrevented).toBeTrue();
    fixture.destroy();
  });

  it('uses the shared keyboard lifecycle for TeamMatchday fights', () => {
    tournamentSignal.update((tournament) => ({ ...tournament, competitionMode: 'TeamMatchday' }));
    getTatamiQueueSpy.and.returnValue(of({
      current: createFight({ status: 'InProgress' }),
      next: null,
      onDeck: null,
      upcoming: [],
    } as TatamiQueue));

    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(pauseFightSpy).toHaveBeenCalledWith('tournament-1', 'fight-1', jasmine.any(String));
    fixture.destroy();
  });

  it('refreshes queue and athlete metadata immediately after a completed fight update', () => {
    const fixture = TestBed.createComponent(MatchComponent);
    fixture.detectChanges();

    getTatamiQueueSpy.calls.reset();
    getAthletesSpy.calls.reset();

    fightUpdates.next(createFight());

    expect(getTatamiQueueSpy).toHaveBeenCalledTimes(1);
    expect(getAthletesSpy).toHaveBeenCalledTimes(1);

    fixture.destroy();
  });

  it('shows and wires the Hiki-wake button for team-matchday fights', () => {
    tournamentSignal.update((tournament) => ({ ...tournament, competitionMode: 'TeamMatchday' }));
    getTatamiQueueSpy.and.returnValue(of({
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
      updatedAtUtc: new Date(startedAt.getTime() + 5_400).toISOString(),
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
      osaeKomiElapsedMilliseconds: 3_400,
    });

    component.restartTimer(pausedHold);

    expect(component.isOsaeKomiPaused(pausedHold)).toBeTrue();
    expect(component.osaeKomiToggleLabelKey()).toBe('match.resumeOsae');
    expect(component.holdTimerLabel()).toBe('3.4s / 20s');

    const runningHold = createFight({
      status: 'InProgress',
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date().toISOString(),
      osaeKomiPausedAtUtc: null,
      osaeKomiElapsedMilliseconds: 3_400,
    });

    component.restartTimer(runningHold);

    expect(component.isOsaeKomiPaused(runningHold)).toBeFalse();
    expect(component.osaeKomiToggleLabelKey()).toBe('match.pauseOsae');

    fixture.destroy();
  });
});