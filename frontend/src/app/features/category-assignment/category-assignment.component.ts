import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TournamentContextService } from '../../core/tournament-context.service';
import { I18nService } from '../../core/i18n.service';
import { extractApiError } from '../../core/http-error';
import { AutoAssignResult, Category, Gender, RegistrationDetail } from '../../core/models';

/**
 * Category assignment step between Meldungen and Auslosung.
 * Provides bulk auto-assignment based on gender, birth year and weight, plus
 * per-athlete manual override via a category dropdown.
 */
@Component({
  selector: 'app-category-assignment',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './category-assignment.component.html',
})
export class CategoryAssignmentComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);

  protected readonly registrations = signal<RegistrationDetail[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly loading = signal(false);
  protected readonly autoAssigning = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly autoAssignResult = signal<AutoAssignResult | null>(null);

  /** Registrations grouped by category id; key '' = unassigned. */
  protected readonly grouped = computed(() => {
    const map = new Map<string, RegistrationDetail[]>();
    map.set('', []);
    for (const cat of this.categories()) {
      map.set(cat.id, []);
    }
    for (const reg of this.registrations()) {
      const key = reg.categoryId ?? '';
      if (!map.has(key)) {
        map.set(key, []);
      }
      map.get(key)!.push(reg);
    }
    return map;
  });

  protected readonly assignedCategories = computed(() =>
    this.categories().filter((c) => (this.grouped().get(c.id)?.length ?? 0) > 0));

  protected readonly unassigned = computed(() => this.grouped().get('') ?? []);

  ngOnInit(): void {
    if (this.context.tournamentId()) {
      this.load();
    }
  }

  protected get tournamentId(): string | null {
    return this.context.tournamentId();
  }

  protected genderLabel(g: Gender): string {
    return this.i18n.translate(g === 'Male' ? 'gender.male' : 'gender.female');
  }

  protected weightLabel(kg: number | null): string {
    return kg !== null ? `-${kg} kg` : this.i18n.translate('categories.weightOpen');
  }

  protected load(): void {
    const id = this.tournamentId;
    if (!id) return;
    this.loading.set(true);
    this.error.set(null);
    this.autoAssignResult.set(null);
    this.api.getCategories(id).subscribe({ next: (x) => this.categories.set(x) });
    this.api.getRegistrations(id).subscribe({
      next: (x) => {
        this.registrations.set(x);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
  }

  protected autoAssign(): void {
    const id = this.tournamentId;
    if (!id) return;
    this.autoAssigning.set(true);
    this.error.set(null);
    this.autoAssignResult.set(null);
    this.api.autoAssignCategories(id).subscribe({
      next: (result) => {
        this.autoAssignResult.set(result);
        this.autoAssigning.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.save')));
        this.autoAssigning.set(false);
      },
    });
  }

  protected reassign(reg: RegistrationDetail, categoryId: string): void {
    const id = this.tournamentId;
    if (!id) return;
    this.error.set(null);
    this.api.assignCategory(id, reg.id, { categoryId }).subscribe({
      next: () => {
        this.registrations.update((list) =>
          list.map((r) => r.id === reg.id ? { ...r, categoryId } : r));
      },
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.save'))),
    });
  }

  protected categoryLabel(c: Category): string {
    const weight = c.weightClassKg !== null
      ? `-${c.weightClassKg} kg`
      : this.i18n.translate('categories.weightOpen');
    return `${c.name} (${c.ageGroup}, ${this.genderLabel(c.gender)}, ${weight})`;
  }

  protected ageBoundsLabel(c: Category): string {
    if (c.minBirthYear === null && c.maxBirthYear === null) return '';
    if (c.minBirthYear !== null && c.maxBirthYear !== null) {
      return `${c.minBirthYear}–${c.maxBirthYear}`;
    }
    if (c.minBirthYear !== null) return `≥ ${c.minBirthYear}`;
    return `≤ ${c.maxBirthYear}`;
  }
}
