import {
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { ApiService } from '../../core/api.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { Category, MedalEntry, RankingEntry } from '../../core/models';

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
export class ResultsComponent implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly context = inject(TournamentContextService);

  protected readonly activeTab = signal<'rankings' | 'medals'>('rankings');
  protected readonly rankings = signal<CategoryRanking[]>([]);
  protected readonly medalTable = signal<MedalEntry[]>([]);
  protected readonly medalLoading = signal(false);

  ngOnInit(): void {
    const tid = this.context.tournamentId();
    if (!tid) return;

    this.api.getCategories(tid).subscribe(cats => {
      const rows: CategoryRanking[] = cats.map(c => ({ category: c, entries: [], loading: true }));
      this.rankings.set(rows);
      cats.forEach((cat, i) => {
        this.api.getCategoryRankings(tid, cat.id).subscribe({
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

    this.medalLoading.set(true);
    this.api.getMedalTable(tid).subscribe({
      next: m => { this.medalTable.set(m); this.medalLoading.set(false); },
      error: () => this.medalLoading.set(false),
    });
  }

  protected setTab(tab: 'rankings' | 'medals'): void {
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
}
