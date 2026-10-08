import type { Mock } from 'vitest';
import { signal } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { AgeGroupClubScoringResponse, Category, ClubScoringEntry, Fight, GlobalClubScoringResponse, } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { ResultsComponent } from './results.component';

describe('ResultsComponent (Vereinswertung tab)', () => {
  let fightUpdated: Subject<Fight>;
  let categoryFightsUpdated: Subject<{
    tournamentId: string;
    categoryId: string;
  }>;
  let reconnected: Subject<void>;
  let tournamentId: ReturnType<typeof signal<string | null>>;
  let apiSpies: {
    getCategories: Mock;
    getCategoryRankings: Mock;
    getMedalTable: Mock;
    getAgeGroupClubScoring: Mock;
    getGlobalClubScoring: Mock;
    downloadResultsCsv: Mock;
  };

  function clubEntry(overrides: Partial<ClubScoringEntry> = {}): ClubScoringEntry {
    return {
      rank: 1,
      isSharedRank: false,
      clubId: 'club-alpha',
      clubName: 'SC Alpha',
      firstPlaces: 1,
      secondPlaces: 0,
      thirdPlaces: 0,
      basePoints: 7,
      wins: 3,
      fights: 4,
      winRateRaw: 0.75,
      winRateDisplay: 0.75,
      scoreRaw: 5.25,
      scoreDisplay: 5.25,
      ...overrides,
    };
  }

  function ageGroupResponse(): AgeGroupClubScoringResponse {
    return {
      tournamentId: 'tournament-1',
      generatedAtUtc: new Date().toISOString(),
      items: [
        {
          ageGroup: 'U15',
          status: 'Final',
          completedFights: 4,
          plannedFights: 4,
          clubs: [
            clubEntry(),
            clubEntry({
              rank: 2,
              clubId: 'club-beta',
              clubName: 'SC Beta',
              firstPlaces: 0,
              secondPlaces: 1,
              basePoints: 5,
              wins: 1,
              fights: 4,
              winRateDisplay: 0.25,
              scoreDisplay: 1.25,
            }),
          ],
        },
      ],
    };
  }

  function globalResponse(): GlobalClubScoringResponse {
    return {
      tournamentId: 'tournament-1',
      generatedAtUtc: new Date().toISOString(),
      status: 'Provisional',
      completedFights: 2,
      plannedFights: 4,
      clubs: [clubEntry({ wins: 1, fights: 2, winRateDisplay: 0.5, scoreDisplay: 3.5 })],
    };
  }

  beforeEach(() => {
    fightUpdated = new Subject<Fight>();
    categoryFightsUpdated = new Subject<{
      tournamentId: string;
      categoryId: string;
    }>();
    reconnected = new Subject<void>();
    tournamentId = signal<string | null>('tournament-1');

    apiSpies = {
      getCategories: vi.fn().mockName('getCategories').mockReturnValue(of([])),
      getCategoryRankings: vi.fn().mockName('getCategoryRankings').mockReturnValue(of([])),
      getMedalTable: vi.fn().mockName('getMedalTable').mockReturnValue(of([])),
      getAgeGroupClubScoring: vi.fn().mockName('getAgeGroupClubScoring').mockReturnValue(of(ageGroupResponse())),
      getGlobalClubScoring: vi.fn().mockName('getGlobalClubScoring').mockReturnValue(of(globalResponse())),
      downloadResultsCsv: vi.fn().mockName('downloadResultsCsv').mockReturnValue(of(new HttpResponse<Blob>({
        body: new Blob(['csv'], { type: 'text/csv' }),
        headers: new HttpHeaders({
          'Content-Disposition': 'attachment; filename="ergebnisse-test.csv"',
        }),
      }))),
    };

    TestBed.configureTestingModule({
      imports: [ResultsComponent],
      providers: [
        { provide: ApiService, useValue: apiSpies },
        { provide: AuthStateService, useValue: { canOperate: signal(true) } },
        {
          provide: TournamentContextService,
          useValue: { tournamentId },
        },
        {
          provide: TournamentHubService,
          useValue: {
            fightUpdated$: fightUpdated.asObservable(),
            categoryFightsUpdated$: categoryFightsUpdated.asObservable(),
            reconnected$: reconnected.asObservable(),
          },
        },
        {
          provide: I18nService,
          useValue: { translate: (key: string) => key },
        },
      ],
    });
  });

  it('loads age-group and global club scoring on init', () => {
    const fixture = TestBed.createComponent(ResultsComponent);
    fixture.detectChanges();

    expect(apiSpies.getAgeGroupClubScoring).toHaveBeenCalledTimes(1);

    expect(apiSpies.getAgeGroupClubScoring).toHaveBeenCalledWith('tournament-1');
    expect(apiSpies.getGlobalClubScoring).toHaveBeenCalledTimes(1);
    expect(apiSpies.getGlobalClubScoring).toHaveBeenCalledWith('tournament-1');

    const component = fixture.componentInstance as unknown as {
      ageGroupScoring: () => AgeGroupClubScoringResponse['items'];
      globalScoring: () => GlobalClubScoringResponse | null;
    };
    expect(component.ageGroupScoring().length).toBe(1);
    expect(component.globalScoring()?.status).toBe('Provisional');

    fixture.destroy();
  });

  it('renders the core Vereinswertung table fields for the age-group block', () => {
    const fixture = TestBed.createComponent(ResultsComponent);
    fixture.detectChanges();

    (fixture.componentInstance as unknown as {
      setTab: (t: string) => void;
    }).setTab('clubs');
    fixture.detectChanges();

    const html = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('.club-table tbody tr');

    // Age-group table (2 clubs) + global table (1 club) = 3 rows.
    expect(rows.length).toBe(3);

    const firstRow = rows[0];
    const cells = Array.from(firstRow.querySelectorAll('td')).map(c => c.textContent?.trim());
    // rank, club, 1./2./3. places, base points, wins, fights, win rate, score.
    expect(cells).toEqual(['1.', 'SC Alpha', '1', '0', '0', '7', '3', '4', '75.00%', '5.25']);

    // Status badge label for a completed age group must be the "final" key.
    expect(html).toContain('results.status.final');

    fixture.destroy();
  });

  it('shows a provisional status badge for the global block', () => {
    const fixture = TestBed.createComponent(ResultsComponent);
    fixture.detectChanges();

    (fixture.componentInstance as unknown as {
      setTab: (t: string) => void;
    }).setTab('clubs');
    fixture.detectChanges();

    const html = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(html).toContain('results.status.provisional');
    // Progress text "2 / 4" for the global block.
    expect(html).toContain('2 / 4');

    fixture.destroy();
  });

  it('maps status values to translation keys and formats rate/score', () => {
    const fixture = TestBed.createComponent(ResultsComponent);
    const component = fixture.componentInstance as unknown as {
      statusKey: (s: 'Provisional' | 'Final') => string;
      winRatePercent: (e: ClubScoringEntry) => string;
      scoreValue: (e: ClubScoringEntry) => string;
    };

    expect(component.statusKey('Final')).toBe('results.status.final');
    expect(component.statusKey('Provisional')).toBe('results.status.provisional');
    expect(component.winRatePercent(clubEntry({ winRateDisplay: 0.3333 }))).toBe('33.33%');
    expect(component.scoreValue(clubEntry({ scoreDisplay: 4 }))).toBe('4.00');

    fixture.destroy();
  });

  it('reloads club scoring when a fight update arrives over the hub', () => {
    const fixture = TestBed.createComponent(ResultsComponent);
    fixture.detectChanges();

    expect(apiSpies.getAgeGroupClubScoring).toHaveBeenCalledTimes(1);

    fightUpdated.next({ id: 'fight-1' } as Fight);

    expect(apiSpies.getAgeGroupClubScoring).toHaveBeenCalledTimes(2);
    expect(apiSpies.getGlobalClubScoring).toHaveBeenCalledTimes(2);

    fixture.destroy();
  });

  it('starts the CSV download for the active tournament', () => {
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:results');
    const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockReturnValue(undefined);
    const fixture = TestBed.createComponent(ResultsComponent);
    fixture.detectChanges();

    (fixture.componentInstance as unknown as {
      exportResultsCsv: () => void;
    }).exportResultsCsv();

    expect(apiSpies.downloadResultsCsv).toHaveBeenCalledTimes(1);

    expect(apiSpies.downloadResultsCsv).toHaveBeenCalledWith('tournament-1');
    expect(createObjectUrl).toHaveBeenCalledTimes(1);
    expect(createObjectUrl).toHaveBeenCalledWith(expect.any(Blob));
    expect(click).toHaveBeenCalled();
    expect(revokeObjectUrl).toHaveBeenCalledTimes(1);
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:results');

    fixture.destroy();
  });

  it('shows a localized error when the CSV download fails', () => {
    apiSpies.downloadResultsCsv.mockReturnValue(throwError(() => new Error('download failed')));
    const fixture = TestBed.createComponent(ResultsComponent);
    fixture.detectChanges();

    (fixture.componentInstance as unknown as {
      exportResultsCsv: () => void;
    }).exportResultsCsv();

    const component = fixture.componentInstance as unknown as {
      exportError: () => string | null;
    };
    expect(component.exportError()).toBe('results.exportError');

    fixture.destroy();
  });
});
