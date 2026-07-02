import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { extractApiError } from '../../core/http-error';
import { I18nService } from '../../core/i18n.service';
import { Athlete, Category, Fight, Tatami } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TranslatePipe } from '../../core/translate.pipe';

const OPERATOR_NAME_KEY = 'judo.operatorName';

interface FightAssignmentView {
  fight: Fight;
  categoryName: string;
  whiteName: string;
  blueName: string;
}

@Component({
  selector: 'app-tatami-assignment',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './tatami-assignment.component.html',
  styleUrl: './tatami-assignment.component.css',
})
export class TatamiAssignmentComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);
  protected readonly canOperate = this.auth.canOperate;

  protected readonly categories = signal<Category[]>([]);
  protected readonly tatamis = signal<Tatami[]>([]);
  protected readonly athletes = signal<Map<string, Athlete>>(new Map());
  protected readonly fights = signal<Fight[]>([]);
  protected readonly loading = signal(false);
  protected readonly assigning = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly info = signal<string | null>(null);
  protected readonly operatorName = signal<string>(this.restoreOperatorName());
  protected readonly keepCategoryOnSingleTatami = signal(false);

  protected readonly activeTatamis = computed(() =>
    this.tatamis()
      .filter((t) => t.isActive)
      .sort((a, b) => a.displayOrder - b.displayOrder));

  protected readonly selectableTatamis = computed(() => {
    const assignedTatamiIds = new Set(
      this.assignableFightViews()
        .map((view) => view.fight.tatamiId)
        .filter((tatamiId): tatamiId is string => tatamiId !== null));

    return this.tatamis()
      .filter((tatami) => tatami.isActive || assignedTatamiIds.has(tatami.id))
      .sort((a, b) => a.displayOrder - b.displayOrder);
  });

  protected readonly assignableFightViews = computed(() => {
    const categoryMap = new Map(this.categories().map((c) => [c.id, c.name]));
    return this.fights()
      .filter((f) => this.isAssignable(f))
      .sort((a, b) => {
        if (a.round !== b.round) return a.round - b.round;
        if (a.fightNumber !== b.fightNumber) return a.fightNumber - b.fightNumber;
        return a.categoryId.localeCompare(b.categoryId);
      })
      .map((fight) => ({
        fight,
        categoryName: categoryMap.get(fight.categoryId) ?? this.i18n.translate('tatamiAssignment.unknownCategory'),
        whiteName: this.athleteName(fight.whiteAthleteId),
        blueName: this.athleteName(fight.blueAthleteId),
      } satisfies FightAssignmentView));
  });

  protected readonly assignedCount = computed(() =>
    this.assignableFightViews().filter((x) => x.fight.tatamiId !== null).length);

  ngOnInit(): void {
    if (this.context.tournamentId()) {
      this.load();
    }
  }

  protected get tournamentId(): string | null {
    return this.context.tournamentId();
  }

  protected tatamiName(tatamiId: string | null): string {
    if (!tatamiId) {
      return this.i18n.translate('tatamiAssignment.notAssigned');
    }
    const tatami = this.tatamis().find((t) => t.id === tatamiId);
    return tatami?.name ?? this.i18n.translate('tatamiAssignment.notAssigned');
  }

  protected saveOperatorName(name: string): void {
    this.operatorName.set(name);
    try {
      localStorage.setItem(OPERATOR_NAME_KEY, name);
    } catch {
      // Ignore storage errors and keep in-memory state.
    }
  }

  protected clearMessages(): void {
    this.error.set(null);
    this.info.set(null);
  }

  protected load(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }

    this.loading.set(true);
    this.clearMessages();

    forkJoin({
      categories: this.api.getCategories(id),
      tatamis: this.api.getTatamis(id),
      athletes: this.api.getAthletes(id),
    }).subscribe({
      next: ({ categories, tatamis, athletes }) => {
        this.categories.set(categories);
        this.tatamis.set(tatamis);
        this.athletes.set(new Map(athletes.map((a) => [a.id, a])));
        this.loadFightsForCategories(categories);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
  }

  protected autoAssign(): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id || this.assigning()) {
      return;
    }

    const tatamis = this.activeTatamis();
    if (tatamis.length === 0) {
      this.error.set(this.i18n.translate('tatamiAssignment.noActiveTatamis'));
      return;
    }

    const assignmentCandidates = this.keepCategoryOnSingleTatami()
      ? this.buildCategoryStickyAssignments(this.assignableFightViews(), tatamis)
      : this.assignableFightViews().map((view, index) => ({
        fightId: view.fight.id,
        tatamiId: tatamis[index % tatamis.length].id,
        currentTatamiId: view.fight.tatamiId,
      }));

    const assignments = assignmentCandidates
      .filter((x) => x.currentTatamiId !== x.tatamiId);

    if (assignments.length === 0) {
      this.info.set(this.i18n.translate('tatamiAssignment.alreadyAssigned'));
      return;
    }

    this.assigning.set(true);
    this.clearMessages();

    forkJoin(assignments.map((x) =>
      this.api.assignTatami(id, x.fightId, { tatamiId: x.tatamiId }, this.operatorName()))).subscribe({
      next: () => {
        const assignmentMap = new Map(assignments.map((x) => [x.fightId, x.tatamiId]));
        this.fights.update((fights) =>
          fights.map((fight) => {
            const tatamiId = assignmentMap.get(fight.id);
            return tatamiId !== undefined ? { ...fight, tatamiId } : fight;
          }));
        this.info.set(this.i18n.translate('tatamiAssignment.autoAssignDone', { count: assignments.length }));
        this.assigning.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.save')));
        this.assigning.set(false);
      },
    });
  }

  protected assignFight(fightId: string, tatamiIdValue: string): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id || this.assigning()) {
      return;
    }

    const tatamiId = tatamiIdValue ? tatamiIdValue : null;
    this.clearMessages();

    this.assigning.set(true);
    this.api.assignTatami(id, fightId, { tatamiId }, this.operatorName()).subscribe({
      next: () => {
        this.fights.update((fights) =>
          fights.map((f) => (f.id === fightId ? { ...f, tatamiId } : f)));
        this.assigning.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.save')));
        this.assigning.set(false);
      },
    });
  }

  private loadFightsForCategories(categories: Category[]): void {
    const id = this.tournamentId;
    if (!id) {
      this.loading.set(false);
      return;
    }

    if (categories.length === 0) {
      this.fights.set([]);
      this.loading.set(false);
      return;
    }

    forkJoin(categories.map((c) => this.api.getFights(id, c.id))).subscribe({
      next: (fightGroups) => {
        this.fights.set(fightGroups.flat());
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
  }

  private athleteName(athleteId: string | null): string {
    if (!athleteId) {
      return this.i18n.translate('draw.tbd');
    }
    const athlete = this.athletes().get(athleteId);
    if (!athlete) {
      return this.i18n.translate('draw.tbd');
    }
    return `${athlete.lastName}, ${athlete.firstName}`;
  }

  private isAssignable(fight: Fight): boolean {
    return fight.status === 'Pending'
      && !fight.isBye
      && fight.whiteAthleteId !== null
      && fight.blueAthleteId !== null;
  }

  private buildCategoryStickyAssignments(
    views: FightAssignmentView[],
    tatamis: Tatami[],
  ): Array<{ fightId: string; tatamiId: string; currentTatamiId: string | null }> {
    const categoryTatamiMap = new Map<string, string>();
    let categoryTatamiIndex = 0;

    return views.map((view) => {
      let tatamiId = categoryTatamiMap.get(view.fight.categoryId);
      if (!tatamiId) {
        tatamiId = tatamis[categoryTatamiIndex % tatamis.length].id;
        categoryTatamiMap.set(view.fight.categoryId, tatamiId);
        categoryTatamiIndex++;
      }

      return {
        fightId: view.fight.id,
        tatamiId,
        currentTatamiId: view.fight.tatamiId,
      };
    });
  }

  private restoreOperatorName(): string {
    try {
      return localStorage.getItem(OPERATOR_NAME_KEY) ?? 'Operator';
    } catch {
      return 'Operator';
    }
  }
}
