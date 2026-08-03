import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { extractApiError } from '../../core/http-error';
import { I18nService } from '../../core/i18n.service';
import { AffectedFightSummary, CompletedFightSummary, EditFightResultRequest } from '../../core/models';
import { SideThemeService } from '../../core/side-theme.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TranslatePipe } from '../../core/translate.pipe';

interface FilterOption {
  id: string;
  name: string;
}

interface EditState {
  fightId: string;
  whiteAthleteId: string;
  blueAthleteId: string;
  winnerId: string;
  whiteIpponCount: number;
  whiteWazaAriCount: number;
  whiteYukoCount: number;
  whitePenalties: number;
  blueIpponCount: number;
  blueWazaAriCount: number;
  blueYukoCount: number;
  bluePenalties: number;
  saving: boolean;
  saveError: string | null;
  affectedFights: AffectedFightSummary[] | null;
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
  protected readonly auth = inject(AuthStateService);
  protected readonly context = inject(TournamentContextService);
  protected readonly sideTheme = inject(SideThemeService);

  protected readonly fights = signal<CompletedFightSummary[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly selectedCategoryId = signal<string>('');
  protected readonly selectedTatamiId = signal<string>('');
  protected readonly expanded = signal<Set<string>>(new Set());
  protected readonly editState = signal<EditState | null>(null);

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
    this.editState.set(null);
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
    const current = this.editState();
    if (current?.fightId === fightId) return; // editing row: keep open, don't toggle

    const next = new Set(this.expanded());
    if (next.has(fightId)) {
      next.delete(fightId);
    } else {
      next.add(fightId);
    }
    this.expanded.set(next);
  }

  protected isExpanded(fightId: string): boolean {
    return this.expanded().has(fightId) || this.editState()?.fightId === fightId;
  }

  /** Returns true if the fight can be edited (non-group-stage, Admin user). */
  protected canEdit(fight: CompletedFightSummary): boolean {
    return this.auth.isAdmin() && fight.bracketType !== 'GroupStage';
  }

  protected startEdit(fight: CompletedFightSummary): void {
    this.editState.set({
      fightId: fight.fightId,
      whiteAthleteId: fight.whiteAthleteId ?? '',
      blueAthleteId: fight.blueAthleteId ?? '',
      winnerId: fight.winnerSide === 'White' ? (fight.whiteAthleteId ?? '') : (fight.blueAthleteId ?? ''),
      whiteIpponCount: fight.whiteIpponCount,
      whiteWazaAriCount: fight.whiteWazaAriCount,
      whiteYukoCount: fight.whiteYukoCount,
      whitePenalties: fight.whitePenalties,
      blueIpponCount: fight.blueIpponCount,
      blueWazaAriCount: fight.blueWazaAriCount,
      blueYukoCount: fight.blueYukoCount,
      bluePenalties: fight.bluePenalties,
      saving: false,
      saveError: null,
      affectedFights: null,
    });
    // Ensure the detail row is open
    const next = new Set(this.expanded());
    next.add(fight.fightId);
    this.expanded.set(next);
  }

  protected cancelEdit(): void {
    this.editState.set(null);
  }

  protected setEditCount(field: keyof EditState, value: string): void {
    this.editState.update((s) => s ? { ...s, [field]: Math.max(0, parseInt(value, 10) || 0) } : s);
  }

  protected setEditWinner(winnerId: string): void {
    this.editState.update((s) => s ? { ...s, winnerId } : s);
  }

  protected save(fight: CompletedFightSummary, confirmed: boolean): void {
    const state = this.editState();
    const tournamentId = this.context.tournamentId();
    if (!state || !tournamentId) return;

    const request: EditFightResultRequest = {
      whiteIpponCount: state.whiteIpponCount,
      whiteWazaAriCount: state.whiteWazaAriCount,
      whiteYukoCount: state.whiteYukoCount,
      whitePenalties: state.whitePenalties,
      blueIpponCount: state.blueIpponCount,
      blueWazaAriCount: state.blueWazaAriCount,
      blueYukoCount: state.blueYukoCount,
      bluePenalties: state.bluePenalties,
      winnerId: state.winnerId,
      confirmed,
    };

    this.editState.update((s) => s ? { ...s, saving: true, saveError: null, affectedFights: null } : s);

    this.api.editFightResult(tournamentId, fight.fightId, request).subscribe({
      next: (response) => {
        if (response.status === 204) {
          this.editState.set(null);
          this.load();
        } else if (response.body?.status === 'ConfirmationRequired' && response.body.affectedFights) {
          this.editState.update((s) => s ? {
            ...s, saving: false, affectedFights: response.body!.affectedFights
          } : s);
        } else {
          // Unexpected 2xx without a recognised body → treat as success
          this.editState.set(null);
          this.load();
        }
      },
      error: (err) => {
        this.editState.update((s) => s ? {
          ...s, saving: false,
          saveError: extractApiError(err, this.i18n.translate('combatOverview.edit.saveError'))
        } : s);
      },
    });
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
