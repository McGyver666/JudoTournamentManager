import {
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { ApiService } from '../../core/api.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentHubService } from '../../core/tournament-hub.service';
import { TranslatePipe } from '../../core/translate.pipe';
import {
  AgeGroupClubScoringItem,
  Category,
  ClubScoringEntry,
  GlobalClubScoringResponse,
  MedalEntry,
  RankingEntry,
} from '../../core/models';
import { Subscription } from 'rxjs';

interface CategoryRanking {
  category: Category;
  entries: RankingEntry[];
  loading: boolean;
}

@Component({
  selector: 'app-results',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './results.component.html',
  styleUrl: './results.component.css',
})
export class ResultsComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  protected readonly context = inject(TournamentContextService);
  private readonly hub = inject(TournamentHubService);

  protected readonly activeTab = signal<'rankings' | 'medals' | 'clubs'>('rankings');
  protected readonly rankings = signal<CategoryRanking[]>([]);
  protected readonly medalTable = signal<MedalEntry[]>([]);
  protected readonly medalLoading = signal(false);
  protected readonly clubScoringLoading = signal(false);
  protected readonly ageGroupScoring = signal<AgeGroupClubScoringItem[]>([]);
  protected readonly globalScoring = signal<GlobalClubScoringResponse | null>(null);

  private fightSub?: Subscription;
  private categoryFightsSub?: Subscription;
  private reconnectSub?: Subscription;

  ngOnInit(): void {
    const tid = this.context.tournamentId();
    if (!tid) {
      return;
    }

    this.loadRankings(tid);
    this.loadMedalTable(tid);
    this.loadClubScoring(tid);

    this.fightSub = this.hub.fightUpdated$.subscribe(() => this.refreshClubScoring());
    this.categoryFightsSub = this.hub.categoryFightsUpdated$.subscribe((evt) => {
      if (evt.tournamentId === this.context.tournamentId()) {
        this.refreshClubScoring();
      }
    });
    this.reconnectSub = this.hub.reconnected$.subscribe(() => this.refreshClubScoring());
  }

  ngOnDestroy(): void {
    this.fightSub?.unsubscribe();
    this.categoryFightsSub?.unsubscribe();
    this.reconnectSub?.unsubscribe();
  }

  protected setTab(tab: 'rankings' | 'medals' | 'clubs'): void {
    this.activeTab.set(tab);
  }

  protected printPage(): void {
    window.print();
  }

  protected placeLabel(place: number): string {
    if (place === 1) return '🥇';
    if (place === 2) return '🥈';
    if (place === 3) return '🥉';
    return `${place}.`;
  }

  protected statusKey(status: 'Provisional' | 'Final'): string {
    return status === 'Final' ? 'results.status.final' : 'results.status.provisional';
  }

  protected winRatePercent(entry: ClubScoringEntry): string {
    return `${(entry.winRateDisplay * 100).toFixed(2)}%`;
  }

  protected scoreValue(entry: ClubScoringEntry): string {
    return entry.scoreDisplay.toFixed(2);
  }

  private refreshClubScoring(): void {
    const tid = this.context.tournamentId();
    if (!tid) {
      return;
    }

    this.loadClubScoring(tid);
  }

  private loadRankings(tournamentId: string): void {
    this.api.getCategories(tournamentId).subscribe(cats => {
      const rows: CategoryRanking[] = cats.map(c => ({ category: c, entries: [], loading: true }));
      this.rankings.set(rows);
      cats.forEach((cat, i) => {
        this.api.getCategoryRankings(tournamentId, cat.id).subscribe({
          next: entries => {
            const updated = [...this.rankings()];
            updated[i] = { ...updated[i], entries, loading: false };
            this.rankings.set(updated);
          },
          error: () => {
            const updated = [...this.rankings()];
            updated[i] = { ...updated[i], loading: false };
            this.rankings.set(updated);
          },
        });
      });
    });
  }

  private loadMedalTable(tournamentId: string): void {
    this.medalLoading.set(true);
    this.api.getMedalTable(tournamentId).subscribe({
      next: m => {
        this.medalTable.set(m);
        this.medalLoading.set(false);
      },
      error: () => this.medalLoading.set(false),
    });
  }

  private loadClubScoring(tournamentId: string): void {
    this.clubScoringLoading.set(true);

    let pending = 2;
    const finalize = () => {
      pending -= 1;
      if (pending <= 0) {
        this.clubScoringLoading.set(false);
      }
    };

    this.api.getAgeGroupClubScoring(tournamentId).subscribe({
      next: response => {
        this.ageGroupScoring.set(response.items);
        finalize();
      },
      error: () => {
        this.ageGroupScoring.set([]);
        finalize();
      },
    });

    this.api.getGlobalClubScoring(tournamentId).subscribe({
      next: response => {
        this.globalScoring.set(response);
        finalize();
      },
      error: () => {
        this.globalScoring.set(null);
        finalize();
      },
    });
  }
}
