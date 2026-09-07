import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { I18nService } from '../../core/i18n.service';
import { Athlete, Category, Club, Fight, GlobalClubScoringResponse, Tatami, Tournament, TournamentOverviewStats } from '../../core/models';
import { SideThemeService } from '../../core/side-theme.service';
import { TimeService } from '../../core/time.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { TournamentOverviewComponent } from './tournament-overview.component';

describe('TournamentOverviewComponent', () => {
  const now = new Date('2026-09-07T10:00:00.000Z');

  function createTournament(): Tournament {
    return {
      id: 'tournament-1',
      name: 'Testturnier',
      date: '2026-09-07',
      venue: 'Sporthalle',
      organizer: 'Verein',
      accentSideColor: 'Blue',
      osaeKomiIpponSeconds: 20,
      osaeKomiWazaAriSeconds: 10,
      osaeKomiYukoSeconds: 5,
      osaeKomiYukoEnabled: true,
      minimumRestBetweenFightsSeconds: 180,
      twoThirdPlacesInRoundRobin: false,
      createdAtUtc: now.toISOString(),
      updatedAtUtc: now.toISOString(),
    };
  }

  function createFight(id: string, tatamiId: string): Fight {
    return {
      id,
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
      tatamiId,
      queueOrder: null,
      whiteScore: 0,
      blueScore: 0,
      whitePenalties: 0,
      bluePenalties: 0,
      whiteIpponCount: 1,
      whiteWazaAriCount: 0,
      whiteYukoCount: 0,
      blueIpponCount: 0,
      blueWazaAriCount: 1,
      blueYukoCount: 0,
      pausedAtUtc: null,
      osaeKomiSide: null,
      osaeKomiStartedAtUtc: null,
      osaeKomiPausedAtUtc: null,
      osaeKomiElapsedMilliseconds: 0,
      startedAtUtc: new Date(now.getTime() - 30_000).toISOString(),
      completedAtUtc: null,
      isGoldenScore: false,
      createdAtUtc: now.toISOString(),
      updatedAtUtc: now.toISOString(),
    };
  }

  function createTatami(id: string, name: string): Tatami {
    return {
      id,
      tournamentId: 'tournament-1',
      name,
      displayOrder: Number(id.slice(-1)),
      isActive: true,
      createdAtUtc: now.toISOString(),
      updatedAtUtc: now.toISOString(),
    };
  }

  function configure(activeTournament: Tournament | null): { fightUpdates: Subject<Fight>; context: { tournamentId: ReturnType<typeof signal<string | null>>; tournament: ReturnType<typeof signal<Tournament | null>> } } {
    const fightUpdates = new Subject<Fight>();
    const context = {
      tournamentId: signal(activeTournament?.id ?? null),
      tournament: signal(activeTournament),
    };
    const stats: TournamentOverviewStats = {
      tournamentId: 'tournament-1',
      registeredAthletes: 2,
      clubCount: 1,
      categoryCount: 1,
      fightsCompleted: 0,
      fightsTotal: 1,
      averageFightDurationSeconds: null,
      ipponCount: 1,
    };
    const scoring: GlobalClubScoringResponse = {
      tournamentId: 'tournament-1',
      generatedAtUtc: now.toISOString(),
      status: 'Provisional',
      completedFights: 0,
      plannedFights: 1,
      clubs: [],
    };
    const athletes: Athlete[] = [
      {
        id: 'athlete-white', tournamentId: 'tournament-1', clubId: 'club-1', firstName: 'Ada', lastName: 'Weiß',
        birthYear: 2010, gender: 'Female', licenseId: null, weightKg: 50, grade: 1,
        lastFightDurationSeconds: null, lastFightEndedAtUtc: null, createdAtUtc: now.toISOString(), updatedAtUtc: now.toISOString(),
      },
      {
        id: 'athlete-blue', tournamentId: 'tournament-1', clubId: 'club-1', firstName: 'Ben', lastName: 'Blau',
        birthYear: 2010, gender: 'Male', licenseId: null, weightKg: 50, grade: 1,
        lastFightDurationSeconds: null, lastFightEndedAtUtc: null, createdAtUtc: now.toISOString(), updatedAtUtc: now.toISOString(),
      },
    ];
    const clubs: Club[] = [{
      id: 'club-1', tournamentId: 'tournament-1', name: 'JC Test', contactName: null, contactEmail: null,
      contactPhone: null, createdAtUtc: now.toISOString(), updatedAtUtc: now.toISOString(),
    }];
    const categories: Category[] = [{
      id: 'category-1', tournamentId: 'tournament-1', name: 'U18 -50 kg', ageGroup: 'U18', gender: 'Mixed',
      weightClassKg: 50, minBirthYear: null, maxBirthYear: null, rulesetNotes: null, matchDurationSeconds: 180,
      goldenScoreEnabled: false, goldenScoreDurationSeconds: 180, drawFormat: 'SingleElimination', isLocked: true,
      createdAtUtc: now.toISOString(), updatedAtUtc: now.toISOString(),
    }];
    const tatamis = [createTatami('tatami-1', 'Matte 1'), createTatami('tatami-2', 'Matte 2')];
    const queues = tatamis.map((tatami) => ({
      current: createFight(`fight-${tatami.id}`, tatami.id),
      next: null,
      onDeck: null,
      upcoming: [createFight(`fight-${tatami.id}`, tatami.id)],
    }));

    TestBed.configureTestingModule({
      imports: [TournamentOverviewComponent],
      providers: [
        {
          provide: ApiService,
          useValue: {
            getTournament: () => of(activeTournament),
            getAthletes: () => of(athletes),
            getClubs: () => of(clubs),
            getCategories: () => of(categories),
            getTournamentOverviewStats: () => of(stats),
            getGlobalClubScoring: () => of(scoring),
            getTatamis: () => of(tatamis),
            getTatamiQueue: (_tournamentId: string, tatamiId: string) => of(queues.find((_, index) => tatamis[index].id === tatamiId)),
          },
        },
        { provide: I18nService, useValue: { translate: (key: string) => key } },
        { provide: SideThemeService, useValue: { applyTheme: () => undefined, sideLabelKey: () => 'match.whiteSide' } },
        {
          provide: TournamentHubService,
          useValue: {
            connected: signal(false),
            connect: () => Promise.resolve(),
            fightUpdated$: fightUpdates.asObservable(),
            categoryFightsUpdated$: new Subject().asObservable(),
            serverTimeSync$: new Subject().asObservable(),
            reconnected$: new Subject().asObservable(),
          },
        },
        {
          provide: TimeService,
          useValue: {
            nowMs: () => now.getTime(),
            synchronize: () => Promise.resolve(),
            ingestServerNowUtc: () => undefined,
          },
        },
        { provide: TournamentContextService, useValue: context },
      ],
    });

    return { fightUpdates, context };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('renders a clear empty state when no tournament is selected', () => {
    configure(null);

    const fixture = TestBed.createComponent(TournamentOverviewComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.overview-empty')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('common.noTournamentSelected');

    fixture.destroy();
  });

  it('renders all running tatamis and exposes manual hero selectors while offline', () => {
    configure(createTournament());

    const fixture = TestBed.createComponent(TournamentOverviewComponent);
    fixture.detectChanges();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.overview-connection--offline')).not.toBeNull();
    expect(element.querySelectorAll('.tatami-status-card').length).toBe(2);
    expect(element.querySelectorAll('.hero-selector').length).toBe(2);
    expect(element.querySelector('.overview-page-head.section-head')).not.toBeNull();
    expect(element.querySelectorAll('.fighter-score').length).toBe(8);
    expect(element.textContent).toContain('match.yuko');
    expect(element.querySelector('.hero-fighter--accent h3')?.textContent).toContain('Blau, Ben');
    expect(element.querySelector('.hero-fighter--shiro h3')?.textContent).toContain('Weiß, Ada');

    fixture.destroy();
  });
});
