import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ATHLETE_GRADE_OPTIONS, athleteGradeLabelKey } from '../../core/athlete-grade';
import { AuthStateService } from '../../core/auth-state.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TournamentContextService } from '../../core/tournament-context.service';
import { I18nService } from '../../core/i18n.service';
import { extractApiError } from '../../core/http-error';
import {
  Athlete,
  Category,
  Club,
  CreateAthleteRequest,
  CreateCategoryRequest,
  Gender,
  Tatami,
} from '../../core/models';

type Tab = 'tatamis' | 'categories' | 'clubs' | 'athletes';

/**
 * Configuration workspace for the active tournament. Provides CRUD for
 * tatamis, categories, clubs and athletes via tabbed sections. Conflict
 * responses (locked categories, duplicate clubs, in-use clubs) are surfaced
 * with the localized backend message.
 */
@Component({
  selector: 'app-config',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './config.component.html',
  styleUrl: './config.component.css',
})
export class ConfigComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);
  protected readonly canOperate = this.auth.canOperate;

  protected readonly tab = signal<Tab>('tatamis');
  protected readonly error = signal<string | null>(null);

  protected readonly tatamis = signal<Tatami[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly clubs = signal<Club[]>([]);
  protected readonly athletes = signal<Athlete[]>([]);

  protected readonly clubName = computed(() => {
    const map = new Map(this.clubs().map((c) => [c.id, c.name]));
    return (id: string) => map.get(id) ?? '';
  });

  protected readonly gradeOptions = ATHLETE_GRADE_OPTIONS;

  protected gradeLabel(grade: number): string {
    return this.i18n.translate(athleteGradeLabelKey(grade));
  }

  // Form models per entity.
  protected tatamiForm = { id: null as string | null, name: '', displayOrder: null as number | null, isActive: true };
  protected categoryForm: CreateCategoryRequest & { id: string | null } = this.emptyCategory();
  protected clubForm = { id: null as string | null, name: '' };
  protected athleteForm: CreateAthleteRequest & { id: string | null } = this.emptyAthlete();

  protected showTatamiForm = signal(false);
  protected showCategoryForm = signal(false);
  protected showClubForm = signal(false);
  protected showAthleteForm = signal(false);

  ngOnInit(): void {
    if (this.context.tournamentId()) {
      this.loadAll();
    }
  }

  protected get tournamentId(): string | null {
    return this.context.tournamentId();
  }

  protected setTab(tab: Tab): void {
    this.tab.set(tab);
    this.error.set(null);
  }

  protected genderLabel(g: Gender): string {
    return this.i18n.translate(g === 'Male' ? 'gender.male' : 'gender.female');
  }

  private loadAll(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.api.getTatamis(id).subscribe({ next: (x) => this.tatamis.set(x), error: this.onLoadError });
    this.api.getCategories(id).subscribe({ next: (x) => this.categories.set(x), error: this.onLoadError });
    this.api.getClubs(id).subscribe({ next: (x) => this.clubs.set(x), error: this.onLoadError });
    this.api.getAthletes(id).subscribe({ next: (x) => this.athletes.set(x), error: this.onLoadError });
  }

  private readonly onLoadError = (err: unknown): void =>
    this.error.set(extractApiError(err, this.i18n.translate('errors.load')));

  private readonly onSaveError = (err: unknown): void =>
    this.error.set(extractApiError(err, this.i18n.translate('errors.save')));

  // --- Tatamis -----------------------------------------------------------
  protected newTatami(): void {
    if (!this.canOperate()) {
      return;
    }
    this.tatamiForm = { id: null, name: '', displayOrder: null, isActive: true };
    this.showTatamiForm.set(true);
  }

  protected editTatami(t: Tatami): void {
    if (!this.canOperate()) {
      return;
    }
    this.tatamiForm = { id: t.id, name: t.name, displayOrder: t.displayOrder, isActive: t.isActive };
    this.showTatamiForm.set(true);
  }

  protected saveTatami(): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.error.set(null);
    const f = this.tatamiForm;
    const req: Observable<unknown> = f.id
      ? this.api.updateTatami(id, f.id, {
          name: f.name,
          displayOrder: f.displayOrder ?? 0,
          isActive: f.isActive,
        })
      : this.api.createTatami(id, { name: f.name, displayOrder: f.displayOrder });
    req.subscribe({
      next: () => {
        this.showTatamiForm.set(false);
        this.api.getTatamis(id).subscribe({ next: (x) => this.tatamis.set(x) });
      },
      error: this.onSaveError,
    });
  }

  protected deleteTatami(t: Tatami): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id || !confirm(this.i18n.translate('common.confirmDelete'))) {
      return;
    }
    this.api.deleteTatami(id, t.id).subscribe({
      next: () => this.tatamis.update((list) => list.filter((x) => x.id !== t.id)),
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.delete'))),
    });
  }

  // --- Categories --------------------------------------------------------
  protected newCategory(): void {
    if (!this.canOperate()) {
      return;
    }
    this.categoryForm = this.emptyCategory();
    this.showCategoryForm.set(true);
  }

  protected editCategory(c: Category): void {
    if (!this.canOperate()) {
      return;
    }
    this.categoryForm = {
      id: c.id,
      name: c.name,
      ageGroup: c.ageGroup,
      gender: c.gender,
      weightClassKg: c.weightClassKg,
      minBirthYear: c.minBirthYear,
      maxBirthYear: c.maxBirthYear,
      rulesetNotes: c.rulesetNotes,
      matchDurationSeconds: c.matchDurationSeconds,
      goldenScoreEnabled: c.goldenScoreEnabled,
      goldenScoreDurationSeconds: c.goldenScoreDurationSeconds,
    };
    this.showCategoryForm.set(true);
  }

  protected saveCategory(): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.error.set(null);
    const f = this.categoryForm;
    const body: CreateCategoryRequest = {
      name: f.name,
      ageGroup: f.ageGroup,
      gender: f.gender,
      weightClassKg: f.weightClassKg === null || (f.weightClassKg as unknown) === '' ? null : Number(f.weightClassKg),
      minBirthYear: f.minBirthYear === null || (f.minBirthYear as unknown) === '' ? null : Number(f.minBirthYear),
      maxBirthYear: f.maxBirthYear === null || (f.maxBirthYear as unknown) === '' ? null : Number(f.maxBirthYear),
      rulesetNotes: f.rulesetNotes || null,
      matchDurationSeconds: f.matchDurationSeconds > 0 ? f.matchDurationSeconds : 300,
      goldenScoreEnabled: f.goldenScoreEnabled,
      goldenScoreDurationSeconds: f.goldenScoreDurationSeconds > 0 ? f.goldenScoreDurationSeconds : 180,
    };
    const req: Observable<unknown> = f.id
      ? this.api.updateCategory(id, f.id, body)
      : this.api.createCategory(id, body);
    req.subscribe({
      next: () => {
        this.showCategoryForm.set(false);
        this.api.getCategories(id).subscribe({ next: (x) => this.categories.set(x) });
      },
      error: this.onSaveError,
    });
  }

  protected deleteCategory(c: Category): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id || !confirm(this.i18n.translate('common.confirmDelete'))) {
      return;
    }
    this.api.deleteCategory(id, c.id).subscribe({
      next: () => this.categories.update((list) => list.filter((x) => x.id !== c.id)),
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.delete'))),
    });
  }

  // --- Clubs -------------------------------------------------------------
  protected newClub(): void {
    if (!this.canOperate()) {
      return;
    }
    this.clubForm = { id: null, name: '' };
    this.showClubForm.set(true);
  }

  protected editClub(c: Club): void {
    if (!this.canOperate()) {
      return;
    }
    this.clubForm = { id: c.id, name: c.name };
    this.showClubForm.set(true);
  }

  protected saveClub(): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.error.set(null);
    const f = this.clubForm;
    const req: Observable<unknown> = f.id
      ? this.api.updateClub(id, f.id, { name: f.name })
      : this.api.createClub(id, { name: f.name });
    req.subscribe({
      next: () => {
        this.showClubForm.set(false);
        this.api.getClubs(id).subscribe({ next: (x) => this.clubs.set(x) });
      },
      error: this.onSaveError,
    });
  }

  protected deleteClub(c: Club): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id || !confirm(this.i18n.translate('common.confirmDelete'))) {
      return;
    }
    this.api.deleteClub(id, c.id).subscribe({
      next: () => this.clubs.update((list) => list.filter((x) => x.id !== c.id)),
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.delete'))),
    });
  }

  // --- Athletes ----------------------------------------------------------
  protected newAthlete(): void {
    if (!this.canOperate()) {
      return;
    }
    this.athleteForm = this.emptyAthlete();
    if (this.clubs().length > 0) {
      this.athleteForm.clubId = this.clubs()[0].id;
    }
    this.showAthleteForm.set(true);
  }

  protected editAthlete(a: Athlete): void {
    if (!this.canOperate()) {
      return;
    }
    this.athleteForm = {
      id: a.id,
      clubId: a.clubId,
      firstName: a.firstName,
      lastName: a.lastName,
      birthYear: a.birthYear,
      gender: a.gender,
      licenseId: a.licenseId,
      weightKg: a.weightKg,
      grade: a.grade,
    };
    this.showAthleteForm.set(true);
  }

  protected saveAthlete(allowDuplicate = false): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.error.set(null);
    const f = this.athleteForm;
    const body: CreateAthleteRequest = {
      clubId: f.clubId,
      firstName: f.firstName,
      lastName: f.lastName,
      birthYear: Number(f.birthYear),
      gender: f.gender,
      licenseId: f.licenseId || null,
      weightKg: f.weightKg === null || (f.weightKg as unknown) === '' ? null : Number(f.weightKg),
      grade: Number(f.grade),
    };
    const req: Observable<unknown> = f.id
      ? this.api.updateAthlete(id, f.id, body)
      : this.api.createAthlete(id, body, allowDuplicate);
    req.subscribe({
      next: () => {
        this.showAthleteForm.set(false);
        this.api.getAthletes(id).subscribe({ next: (x) => this.athletes.set(x) });
      },
      error: (err: unknown) => {
        // A duplicate (409) on create offers an override.
        if (!f.id && this.isConflict(err)) {
          if (confirm(this.i18n.translate('athletes.duplicateConfirm'))) {
            this.saveAthlete(true);
            return;
          }
          this.error.set(null);
          return;
        }
        this.onSaveError(err);
      },
    });
  }

  protected deleteAthlete(a: Athlete): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id || !confirm(this.i18n.translate('common.confirmDelete'))) {
      return;
    }
    this.api.deleteAthlete(id, a.id).subscribe({
      next: () => this.athletes.update((list) => list.filter((x) => x.id !== a.id)),
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.delete'))),
    });
  }

  private isConflict(err: unknown): boolean {
    return !!err && typeof err === 'object' && (err as { status?: number }).status === 409;
  }

  private emptyCategory(): CreateCategoryRequest & { id: string | null } {
    return {
      id: null,
      name: '',
      ageGroup: '',
      gender: 'Male',
      weightClassKg: null,
      minBirthYear: null,
      maxBirthYear: null,
      rulesetNotes: null,
      matchDurationSeconds: 300,
      goldenScoreEnabled: false,
      goldenScoreDurationSeconds: 180,
    };
  }

  private emptyAthlete(): CreateAthleteRequest & { id: string | null } {
    return {
      id: null,
      clubId: '',
      firstName: '',
      lastName: '',
      birthYear: new Date().getFullYear() - 15,
      gender: 'Male',
      licenseId: null,
      weightKg: null,
      grade: 1,
    };
  }
}
