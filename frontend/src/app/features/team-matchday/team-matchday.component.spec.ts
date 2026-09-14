import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { Athlete, RegistrationDetail, TeamMatchday } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TeamMatchdayComponent } from './team-matchday.component';

describe('TeamMatchdayComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TeamMatchdayComponent],
      providers: [
        { provide: ApiService, useValue: {} },
        { provide: AuthStateService, useValue: { canOperate: signal(true) } },
        { provide: I18nService, useValue: { translate: (key: string) => key } },
        {
          provide: TournamentContextService,
          useValue: {
            tournamentId: signal('tournament-1'),
            tournament: signal({ competitionMode: 'TeamMatchday' }),
          },
        },
      ],
    });
  });

  it('filters athletes by weight class and duplicate lineup selections when enabled', () => {
    const fixture = TestBed.createComponent(TeamMatchdayComponent);
    const component = fixture.componentInstance as any;
    component.matchday.set(createMatchday());
    component.athletes.set([
      createAthlete('valid', 65),
      createAthlete('too-heavy', 67),
      createAthlete('already-selected', 60),
    ]);
    component.registrations.set([
      { athleteId: 'valid' },
      { athleteId: 'too-heavy' },
      { athleteId: 'already-selected' },
    ] as RegistrationDetail[]);
    component.lineupEncounterId.set('encounter-1');
    component.homeLineupAthleteIds.set(['', 'already-selected', '', '', '']);

    expect(component.athletesForLineupTeam('home-team', 0).map((athlete: Athlete) => athlete.id))
      .toEqual(['valid', 'too-heavy', 'already-selected']);

    component.filterLineupAthletes.set(true);

    expect(component.athletesForLineupTeam('home-team', 0).map((athlete: Athlete) => athlete.id))
      .toEqual(['valid']);

    fixture.destroy();
  });
});

function createMatchday(): TeamMatchday {
  return {
    tournamentId: 'tournament-1',
    profile: 'SeniorMen',
    teams: [
      {
        id: 'home-team',
        tournamentId: 'tournament-1',
        clubId: 'club-1',
        name: 'Heim',
        createdAtUtc: '',
        updatedAtUtc: '',
      },
      {
        id: 'away-team',
        tournamentId: 'tournament-1',
        clubId: 'club-2',
        name: 'Gast',
        createdAtUtc: '',
        updatedAtUtc: '',
      },
    ],
    weightClassOrder: [0, 1, 2, 3, 4],
    encounters: [{
      id: 'encounter-1',
      tournamentId: 'tournament-1',
      homeTeamId: 'home-team',
      awayTeamId: 'away-team',
      tatamiId: null,
      displayOrder: 1,
      noShowTeamId: null,
      createdAtUtc: '',
      homeIndividualWins: 0,
      awayIndividualWins: 0,
      homeUnderScore: 0,
      awayUnderScore: 0,
      homeTeamPoints: 0,
      awayTeamPoints: 0,
      outcome: null,
    }],
  };
}

function createAthlete(id: string, weightKg: number): Athlete {
  return {
    id,
    tournamentId: 'tournament-1',
    clubId: 'club-1',
    firstName: id,
    lastName: 'Test',
    birthYear: 1990,
    gender: 'Male',
    licenseId: null,
    weightKg,
    grade: 1,
    lastFightDurationSeconds: null,
    lastFightEndedAtUtc: null,
    createdAtUtc: '',
    updatedAtUtc: '',
  };
}