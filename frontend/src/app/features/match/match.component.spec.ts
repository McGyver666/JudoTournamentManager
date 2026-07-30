import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, Subject } from 'rxjs';
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

  function createTournament(): Tournament {
    return {
      id: 'tournament-1',
      name: 'Testturnier',
      date: '2026-07-27',
      venue: 'Halle 1',
      organizer: 'Club',
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
    };

    getTatamiQueueSpy = apiMock.getTatamiQueue as jasmine.Spy;
    getAthletesSpy = apiMock.getAthletes as jasmine.Spy;

    TestBed.configureTestingModule({
      providers: [
        { provide: ApiService, useValue: apiMock },
        { provide: AuthStateService, useValue: { canOperate: signal(true) } },
        {
          provide: TournamentContextService,
          useValue: {
            tournamentId: signal('tournament-1'),
            tournament: signal(tournament),
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
        { provide: SideThemeService, useValue: { applyTheme: () => undefined } },
        { provide: TimeService, useValue: { synchronize: () => Promise.resolve(), synchronizeIfStale: () => Promise.resolve(), ingestServerNowUtc: () => undefined, nowMs: () => Date.now() } },
        { provide: I18nService, useValue: { translate: (key: string) => key } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({})), queryParamMap: of(convertToParamMap({ tatamiId: 'tatami-1' })) } },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true)) } },
      ],
    });
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
});