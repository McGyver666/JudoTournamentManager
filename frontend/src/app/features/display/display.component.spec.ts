import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, Subject } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { I18nService } from '../../core/i18n.service';
import { Fight, Tatami } from '../../core/models';
import { SideThemeService } from '../../core/side-theme.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { DisplayComponent } from './display.component';

describe('DisplayComponent', () => {
  let fightUpdates: Subject<Fight>;
  let serverTimeSync: Subject<string>;
  let reconnected: Subject<void>;
  let apiCalls: {
    getTournament: jasmine.Spy;
    getAthletes: jasmine.Spy;
    getClubs: jasmine.Spy;
    getCategories: jasmine.Spy;
    getTatamis: jasmine.Spy;
    getTatamiQueue: jasmine.Spy;
    getFights: jasmine.Spy;
    getCategoryStandings: jasmine.Spy;
  };

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
      status: 'InProgress',
      tatamiId: 'tatami-1',
      queueOrder: null,
      whiteScore: 0,
      blueScore: 0,
      whitePenalties: 0,
      bluePenalties: 0,
      whiteIpponCount: 0,
      whiteWazaAriCount: 0,
      whiteYukoCount: 0,
      blueIpponCount: 0,
      blueWazaAriCount: 0,
      blueYukoCount: 0,
      pausedAtUtc: null,
      osaeKomiSide: null,
      osaeKomiStartedAtUtc: null,
      startedAtUtc: new Date(Date.now() - 60_000).toISOString(),
      completedAtUtc: null,
      isGoldenScore: false,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
      ...overrides,
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

  beforeEach(() => {
    fightUpdates = new Subject<Fight>();
    serverTimeSync = new Subject<string>();
    reconnected = new Subject<void>();

    apiCalls = {
      getTournament: jasmine.createSpy('getTournament').and.returnValue(of({ name: 'Testturnier' })),
      getAthletes: jasmine.createSpy('getAthletes').and.returnValue(of([])),
      getClubs: jasmine.createSpy('getClubs').and.returnValue(of([])),
      getCategories: jasmine.createSpy('getCategories').and.returnValue(of([])),
      getTatamis: jasmine.createSpy('getTatamis').and.returnValue(of([])),
      getTatamiQueue: jasmine.createSpy('getTatamiQueue').and.returnValue(of({ current: null, upcoming: [] })),
      getFights: jasmine.createSpy('getFights').and.returnValue(of([])),
      getCategoryStandings: jasmine.createSpy('getCategoryStandings').and.returnValue(of([])),
    };

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ApiService,
          useValue: {
            getServerTime: () => of({ serverTimeUtc: new Date().toISOString() }),
            getGuestShare: () =>
              of({
                tournamentId: 'tournament-1',
                exists: false,
                isEnabled: false,
                isActive: false,
                token: null,
                expiresAtUtc: null,
                publicUrl: null,
              }),
            getGuestShareQr: () => of(''),
            ...apiCalls,
          },
        },
        {
          provide: I18nService,
          useValue: { translate: (key: string) => key },
        },
        {
          provide: SideThemeService,
          useValue: { applyTheme: () => undefined },
        },
        {
          provide: TournamentHubService,
          useValue: {
            connected: signal(false),
            connect: () => Promise.resolve(),
            fightUpdated$: fightUpdates.asObservable(),
            serverTimeSync$: serverTimeSync.asObservable(),
            reconnected$: reconnected.asObservable(),
            categoryFightsUpdated$: new Subject().asObservable(),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({})),
            queryParamMap: of(convertToParamMap({})),
          },
        },
      ],
    });
  });

  it('keeps a stopped Osae-Komi visible until a new Osae-Komi starts', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date(Date.now() - 5_000).toISOString(),
    });
    const stoppedFight = createFight();
    const restartedHold = createFight({
      osaeKomiSide: 'Blue',
      osaeKomiStartedAtUtc: new Date(Date.now() - 2_000).toISOString(),
    });

    (fixture.componentInstance as any).displays.set([
      { tatami: createTatami(), current: activeHold, nextFights: [] },
    ]);

    fightUpdates.next(activeHold);

    expect((fixture.componentInstance as any).isOsaeKomiRunning(activeHold)).toBeTrue();

    fightUpdates.next(stoppedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(stoppedFight)).toBeTrue();
    expect((fixture.componentInstance as any).osaeKomiSideLabel(stoppedFight)).toBe('white');

    fightUpdates.next(restartedHold);

    const displayedFight = (fixture.componentInstance as any).displays()[0].current as Fight;

    expect((fixture.componentInstance as any).isOsaeKomiRunning(displayedFight)).toBeTrue();
    expect((fixture.componentInstance as any).osaeKomiSideLabel(displayedFight)).toBe('blue');

    fixture.destroy();
  });

  it('keeps a stopped Osae-Komi visible through pause and clears it on resume', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date(Date.now() - 5_000).toISOString(),
    });
    const stoppedFight = createFight();
    const pausedFight = createFight({
      status: 'Paused',
      pausedAtUtc: new Date().toISOString(),
    });
    const resumedFight = createFight();

    (fixture.componentInstance as any).displays.set([
      { tatami: createTatami(), current: activeHold, nextFights: [] },
    ]);

    fightUpdates.next(activeHold);
    fightUpdates.next(stoppedFight);
    fightUpdates.next(pausedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(pausedFight)).toBeTrue();

    fightUpdates.next(resumedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(resumedFight)).toBeFalse();

    fixture.destroy();
  });

  it('refreshes the tournament view when a fight is completed', () => {
    apiCalls.getTatamis.and.returnValue(of([createTatami()]));
    apiCalls.getTatamiQueue.and.returnValue(of({ current: createFight(), upcoming: [] }));

    TestBed.overrideProvider(ActivatedRoute, {
      useValue: {
        paramMap: of(convertToParamMap({})),
        queryParamMap: of(convertToParamMap({ tournamentId: 'tournament-1' })),
      },
    });

    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    expect(apiCalls.getTatamiQueue).toHaveBeenCalledTimes(1);
    expect(apiCalls.getCategories).toHaveBeenCalledTimes(1);
    expect(apiCalls.getFights).not.toHaveBeenCalled();

    fightUpdates.next(createFight({ status: 'Completed', completedAtUtc: new Date().toISOString() }));

    expect(apiCalls.getTatamiQueue).toHaveBeenCalledTimes(2);
    expect(apiCalls.getCategories).toHaveBeenCalledTimes(1);
    expect(apiCalls.getFights).toHaveBeenCalledTimes(0);

    fixture.destroy();
  });

  it('keeps a running Osae-Komi visible when the fight is paused and clears it on resume', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date(Date.now() - 5_000).toISOString(),
    });
    const pausedFight = createFight({
      status: 'Paused',
      pausedAtUtc: new Date().toISOString(),
    });
    const resumedFight = createFight();
    (fixture.componentInstance as any).displays.set([
      { tatami: createTatami(), current: activeHold, nextFights: [] },
    ]);

    fightUpdates.next(activeHold);

    expect((fixture.componentInstance as any).isOsaeKomiRunning(activeHold)).toBeTrue();

    fightUpdates.next(pausedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(pausedFight)).toBeTrue();

    fightUpdates.next(resumedFight);

    const displayedFight = (fixture.componentInstance as any).displays()[0].current as Fight;

    expect((fixture.componentInstance as any).isOsaeKomiRunning(displayedFight)).toBeFalse();
    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(resumedFight)).toBeFalse();

    fixture.destroy();
  });

  it('keeps the snapshot cleared across repeated resumed updates after pause', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date(Date.now() - 5_000).toISOString(),
    });
    const pausedFight = createFight({
      status: 'Paused',
      pausedAtUtc: new Date().toISOString(),
    });
    const resumedFight = createFight();

    (fixture.componentInstance as any).displays.set([
      { tatami: createTatami(), current: activeHold, nextFights: [] },
    ]);

    fightUpdates.next(activeHold);
    fightUpdates.next(pausedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(pausedFight)).toBeTrue();

    fightUpdates.next(resumedFight);
    (fixture.componentInstance as any).updateDisplayedFight(resumedFight);

    const displayedFight = (fixture.componentInstance as any).displays()[0].current as Fight;

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(displayedFight)).toBeFalse();
    expect((fixture.componentInstance as any).osaeKomiSideLabel(displayedFight)).toBeNull();

    fixture.destroy();
  });
});