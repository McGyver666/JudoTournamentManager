import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { extractApiError } from '../../core/http-error';
import { I18nService } from '../../core/i18n.service';
import { CompletedFightSummary } from '../../core/models';
import { SideThemeService } from '../../core/side-theme.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TranslatePipe } from '../../core/translate.pipe';

interface FilterOption {
  id: string;
  name: string;
}

/**
 * Tournament-wide combat overview (Kampfübersicht): lists every completed, non-bye fight
 * with all stored details. Loads once on open; refreshes on demand.
 */
@Component({
  selector: 'app-combat-overview',
  standalone: true,
  imports: [DatePipe, FormsModule, TranslatePipe],
  templateUrl: './combat-overview.component.html',
  styleUrl: './combat-overview.component.css',
})
export class CombatOverviewComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);
  protected readonly sideTheme = inject(SideThemeService);

  protected readonly fights = signal<CompletedFightSummary[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly selectedCategoryId = signal<string>('');
  protected readonly selectedTatamiId = signal<string>('');
  protected readonly expanded = signal<Set<string>>(new Set());

  protected readonly accentLabelKey = computed(() =>
    this.sideTheme.accentSideLabelKey(this.context.tournament()));

  protected readonly categoryFilters = computed<FilterOption[]>(() => {
    const map = new Map<string, string>();
    for (const f of this.fights()) {
      map.set(f.categoryId, f.categoryName);
    }
    return Array.from(map, ([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });

  protected readonly tatamiFilters = computed<FilterOption[]>(() => {
    const map = new Map<string, string>();
    for (const f of this.fights()) {
      if (f.tatamiId && f.tatamiName) {
        map.set(f.tatamiId, f.tatamiName);
      }
    }
    return Array.from(map, ([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });

  protected readonly filteredFights = computed(() => {
    const category = this.selectedCategoryId();
    const tatami = this.selectedTatamiId();
    return this.fights().filter((f) =>
      (category === '' || f.categoryId === category) &&
      (tatami === '' || f.tatamiId === tatami));
  });

  ngOnInit(): void {
    if (this.context.tournamentId()) {
      this.load();
    }
  }

  protected load(): void {
    const id = this.context.tournamentId();
    if (!id) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api.getCompletedFights(id).subscribe({
      next: (fights) => {
        this.fights.set(fights);
        this.expanded.set(new Set());
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
  }

  protected toggle(fightId: string): void {
    const next = new Set(this.expanded());
    if (next.has(fightId)) {
      next.delete(fightId);
    } else {
      next.add(fightId);
    }
    this.expanded.set(next);
  }

  protected isExpanded(fightId: string): boolean {
    return this.expanded().has(fightId);
  }

  protected bracketTypeKey(fight: CompletedFightSummary): string {
    switch (fight.bracketType) {
      case 'Repechage':
        return 'combatOverview.bracket.repechage';
      case 'GroupStage':
        return 'combatOverview.bracket.groupStage';
      default:
        return 'combatOverview.bracket.main';
    }
  }

  protected formatDuration(seconds: number | null): string {
    if (seconds === null) {
      return '–';
    }
    const minutes = Math.floor(seconds / 60);
    const remaining = seconds % 60;
    return `${minutes}:${remaining.toString().padStart(2, '0')}`;
  }
}
