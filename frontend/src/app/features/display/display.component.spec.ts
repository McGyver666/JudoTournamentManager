import type { Mock } from 'vitest';
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
    getTournament: Mock;
    getAthletes: Mock;
    getClubs: Mock;
    getCategories: Mock;
    getTatamis: Mock;
    getTatamiQueue: Mock;
    getFights: Mock;
    getCategoryStandings: Mock;
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
      osaeKomiPausedAtUtc: null,
      osaeKomiElapsedMilliseconds: 0,
      startedAtUtc: new Date(Date.now() - 60000).toISOString(),
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
      getTournament: vi.fn().mockName('getTournament').mockReturnValue(of({ name: 'Testturnier' })),
      getAthletes: vi.fn().mockName('getAthletes').mockReturnValue(of([])),
      getClubs: vi.fn().mockName('getClubs').mockReturnValue(of([])),
      getCategories: vi.fn().mockName('getCategories').mockReturnValue(of([])),
      getTatamis: vi.fn().mockName('getTatamis').mockReturnValue(of([])),
      getTatamiQueue: vi.fn().mockName('getTatamiQueue').mockReturnValue(of({ current: null, upcoming: [] })),
      getFights: vi.fn().mockName('getFights').mockReturnValue(of([])),
      getCategoryStandings: vi.fn().mockName('getCategoryStandings').mockReturnValue(of([])),
    };

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ApiService,
          useValue: {
            getServerTime: () => of({ serverTimeUtc: new Date().toISOString() }),
            getGuestShare: () => of({
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
      osaeKomiStartedAtUtc: new Date(Date.now() - 5000).toISOString(),
    });
    const stoppedFight = createFight();
    const restartedHold = createFight({
      osaeKomiSide: 'Blue',
      osaeKomiStartedAtUtc: new Date(Date.now() - 2000).toISOString(),
    });

    (fixture.componentInstance as any).displays.set([
      { tatami: createTatami(), current: activeHold, nextFights: [] },
    ]);

    fightUpdates.next(activeHold);

    expect((fixture.componentInstance as any).isOsaeKomiRunning(activeHold)).toBe(true);

    fightUpdates.next(stoppedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(stoppedFight)).toBe(true);
    expect((fixture.componentInstance as any).osaeKomiSideLabel(stoppedFight)).toBe('white');

    fightUpdates.next(restartedHold);

    const displayedFight = (fixture.componentInstance as any).displays()[0].current as Fight;

    expect((fixture.componentInstance as any).isOsaeKomiRunning(displayedFight)).toBe(true);
    expect((fixture.componentInstance as any).osaeKomiSideLabel(displayedFight)).toBe('blue');

    fixture.destroy();
  });

  it('uses the server mutation timestamp when freezing a stopped Osae-Komi', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const startedAt = new Date('2026-09-04T10:00:00.000Z');
    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: startedAt.toISOString(),
    });
    const stoppedFight = createFight({
      updatedAtUtc: new Date(startedAt.getTime() + 5400).toISOString(),
    });

    (fixture.componentInstance as any).displays.set([
      { tatami: createTatami(), current: activeHold, nextFights: [] },
    ]);

    fightUpdates.next(activeHold);
    fightUpdates.next(stoppedFight);

    expect((fixture.componentInstance as any).osaeKomiSecondsLabel(stoppedFight)).toBe('5.4s');

    fixture.destroy();
  });

  it('shows a server-persisted paused Osae-Komi with its accumulated duration', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance as any;
    const pausedHold = createFight({
      status: 'InProgress',
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: null,
      osaeKomiPausedAtUtc: new Date().toISOString(),
      osaeKomiElapsedMilliseconds: 3400,
    });

    expect(component.isOsaeKomiPaused(pausedHold)).toBe(true);
    expect(component.osaeKomiSecondsLabel(pausedHold)).toBe('3.4s');

    fixture.destroy();
  });

  it('keeps the cap that applied before a stop awarded Waza-ari', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance as any;
    component.tournament.set({
      osaeKomiIpponSeconds: 20,
      osaeKomiWazaAriSeconds: 10,
    });

    const startedAt = new Date('2026-09-04T10:00:00.000Z');
    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: startedAt.toISOString(),
      whiteWazaAriCount: 0,
    });
    const stoppedFight = createFight({
      updatedAtUtc: new Date(startedAt.getTime() + 10000).toISOString(),
      whiteWazaAriCount: 1,
    });

    component.displays.set([
      { tatami: createTatami(), current: activeHold, nextFights: [] },
    ]);

    fightUpdates.next(activeHold);
    fightUpdates.next(stoppedFight);

    expect(component.osaeKomiCapSecondsLabel(stoppedFight)).toBe('20s');

    fixture.destroy();
  });

  it('keeps a stopped Osae-Komi visible through pause and clears it on resume', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date(Date.now() - 5000).toISOString(),
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

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(pausedFight)).toBe(true);

    fightUpdates.next(resumedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(resumedFight)).toBe(false);

    fixture.destroy();
  });

  function renderWithHansokuMake(paramMap: Record<string, string>): HTMLElement {
    apiCalls.getTatamis.mockReturnValue(of([createTatami()]));
    apiCalls.getTatamiQueue.mockReturnValue(of({ current: createFight({ bluePenalties: 3, whiteIpponCount: 1 }), upcoming: [] }));
    TestBed.overrideProvider(ActivatedRoute, {
      useValue: {
        paramMap: of(convertToParamMap(paramMap)),
        queryParamMap: of(convertToParamMap({ tournamentId: 'tournament-1' })),
      },
    });
    TestBed.overrideProvider(SideThemeService, {
      useValue: { applyTheme: () => undefined, accentSideLabelKey: () => 'match.blueSide' },
    });

    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('replaces the shido bubbles with a Hansoku-make badge on the tatami screen', () => {
    const root = renderWithHansokuMake({ tatamiId: 'tatami-1' });

    const blue = root.querySelector('.tatami-athlete--blue') as HTMLElement;
    const white = root.querySelector('.tatami-athlete--white') as HTMLElement;
    expect(blue.querySelector('.hansoku-make-badge')?.textContent).toContain('match.hansokuMake');
    expect(blue.querySelectorAll('.shido-bubble').length).toBe(0);
    expect(white.querySelector('.hansoku-make-badge')).toBeNull();
    expect(white.querySelectorAll('.shido-bubble').length).toBe(3);
  });

  it('replaces the shido bubbles with a Hansoku-make badge on the overview grid', () => {
    const root = renderWithHansokuMake({});

    const blue = root.querySelector('.athlete-col--blue-left') as HTMLElement;
    const white = root.querySelector('.athlete-col--white-right') as HTMLElement;
    expect(blue.querySelector('.hansoku-make-badge')?.textContent).toContain('match.hansokuMake');
    expect(blue.querySelectorAll('.shido-bubble').length).toBe(0);
    expect(white.querySelector('.hansoku-make-badge')).toBeNull();
    expect(white.querySelectorAll('.shido-bubble').length).toBe(3);
  });

  const clockCases: {
    name: string;
    fight: Partial<Fight>;
    stopped: boolean;
  }[] = [
      { name: 'running fight', fight: {}, stopped: false },
      { name: 'paused fight', fight: { status: 'Paused', pausedAtUtc: new Date().toISOString() }, stopped: true },
      { name: 'paused golden score', fight: { status: 'Paused', pausedAtUtc: new Date().toISOString(), isGoldenScore: true }, stopped: true },
      { name: 'paused osae-komi', fight: { osaeKomiSide: 'Blue', osaeKomiPausedAtUtc: new Date().toISOString() }, stopped: true },
      { name: 'not yet started fight', fight: { status: 'Pending', startedAtUtc: null }, stopped: false },
    ];

  clockCases.forEach(({ name, fight, stopped }) => {
    it(`marks the tatami clock as stopped=${stopped} for a ${name}`, () => {
      apiCalls.getTatamis.mockReturnValue(of([createTatami()]));
      apiCalls.getTatamiQueue.mockReturnValue(of({ current: createFight(fight), upcoming: [] }));
      TestBed.overrideProvider(ActivatedRoute, {
        useValue: {
          paramMap: of(convertToParamMap({ tatamiId: 'tatami-1' })),
          queryParamMap: of(convertToParamMap({ tournamentId: 'tournament-1' })),
        },
      });
      TestBed.overrideProvider(SideThemeService, {
        useValue: { applyTheme: () => undefined, accentSideLabelKey: () => 'match.blueSide' },
      });

      const fixture = TestBed.createComponent(DisplayComponent);
      fixture.detectChanges();
      const root = fixture.nativeElement as HTMLElement;

      const block = root.querySelector('.fight-timer-block') as HTMLElement;
      expect(block.classList.contains('fight-timer-block--stopped')).toBe(stopped);
      expect(root.querySelector('.fight-timer--xl')!.classList.contains('fight-timer--paused')).toBe(stopped);
      expect(root.querySelector('.fight-timer-stopped-label')?.textContent?.trim() ?? null)
        .toBe(stopped ? 'display.timeStopped' : null);

      fixture.destroy();
    });
  });

  it('refreshes the tournament view when a fight is completed', () => {
    apiCalls.getTatamis.mockReturnValue(of([createTatami()]));
    apiCalls.getTatamiQueue.mockReturnValue(of({ current: createFight(), upcoming: [] }));

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
      osaeKomiStartedAtUtc: new Date(Date.now() - 5000).toISOString(),
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

    expect((fixture.componentInstance as any).isOsaeKomiRunning(activeHold)).toBe(true);

    fightUpdates.next(pausedFight);

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(pausedFight)).toBe(true);

    fightUpdates.next(resumedFight);

    const displayedFight = (fixture.componentInstance as any).displays()[0].current as Fight;

    expect((fixture.componentInstance as any).isOsaeKomiRunning(displayedFight)).toBe(false);
    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(resumedFight)).toBe(false);

    fixture.destroy();
  });

  it('keeps the snapshot cleared across repeated resumed updates after pause', () => {
    const fixture = TestBed.createComponent(DisplayComponent);
    fixture.detectChanges();

    const activeHold = createFight({
      osaeKomiSide: 'White',
      osaeKomiStartedAtUtc: new Date(Date.now() - 5000).toISOString(),
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

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(pausedFight)).toBe(true);

    fightUpdates.next(resumedFight);
    (fixture.componentInstance as any).updateDisplayedFight(resumedFight);

    const displayedFight = (fixture.componentInstance as any).displays()[0].current as Fight;

    expect((fixture.componentInstance as any).hasPersistedOsaeKomi(displayedFight)).toBe(false);
    expect((fixture.componentInstance as any).osaeKomiSideLabel(displayedFight)).toBeNull();

    fixture.destroy();
  });
});
