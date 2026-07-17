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
  CategoryGenerationApplyResponse,
  CategoryGenerationGenderMode,
  CategoryGenerationGroupSetting,
  CategoryGenerationPreviewResponse,
  CategoryGenerationWeightMode,
  Category,
  CategoryPreset,
  CategoryPresetItemRequest,
  Club,
  CreateAthleteRequest,
  CreateCategoryRequest,
  GenerateCategoriesRequest,
  Gender,
  GeneratedCategoryProposal,
  RegistrationDetail,
  Tatami,
} from '../../core/models';

type Tab = 'tatamis' | 'categories' | 'clubs' | 'athletes' | 'presets';
type GeneratorStep = 'base' | 'strategy' | 'preview';

interface GenerationAgeGroupRange {
  ageGroup: string;
  minBirthYear: number | null;
  maxBirthYear: number | null;
}

interface GenerationGroupSettingForm {
  ageGroup: string;
  genderMode: CategoryGenerationGenderMode;
  targetAthletesPerCategory: number;
  maxWeightDeviationKg: number;
}

const GENERATION_AGE_GROUP_RANGES: ReadonlyArray<GenerationAgeGroupRange> = [
  { ageGroup: 'U11', minBirthYear: 2016, maxBirthYear: 2018 },
  { ageGroup: 'U13', minBirthYear: 2014, maxBirthYear: 2016 },
  { ageGroup: 'U15', minBirthYear: 2012, maxBirthYear: 2014 },
  { ageGroup: 'U18', minBirthYear: 2009, maxBirthYear: 2011 },
  { ageGroup: 'U21', minBirthYear: 2006, maxBirthYear: 2009 },
  { ageGroup: 'Senioren', minBirthYear: 2009, maxBirthYear: null },
];

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
  protected readonly athleteImportInfo = signal<string | null>(null);

  protected readonly tatamis = signal<Tatami[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly clubs = signal<Club[]>([]);
  protected readonly athletes = signal<Athlete[]>([]);
  protected readonly registrations = signal<RegistrationDetail[]>([]);
  protected readonly presets = signal<CategoryPreset[]>([]);
  protected presetsDirty = false;

  protected readonly clubName = computed(() => {
    const map = new Map(this.clubs().map((c) => [c.id, c.name]));
    return (id: string) => map.get(id) ?? '';
  });

  protected readonly gradeOptions = ATHLETE_GRADE_OPTIONS;

  protected readonly assignedAthleteCountByCategory = computed(() => {
    const grouped = new Map<string, number>();

    for (const registration of this.registrations()) {
      if (!registration.categoryId) {
        continue;
      }

      const current = grouped.get(registration.categoryId) ?? 0;
      grouped.set(registration.categoryId, current + 1);
    }

    return (categoryId: string) => grouped.get(categoryId) ?? 0;
  });

  protected gradeLabel(grade: number): string {
    return this.i18n.translate(athleteGradeLabelKey(grade));
  }

  // Form models per entity.
  protected tatamiForm = { id: null as string | null, name: '', displayOrder: null as number | null, isActive: true };
  protected categoryForm: CreateCategoryRequest & { id: string | null } = this.emptyCategory();
  protected clubForm = { id: null as string | null, name: '', contactName: '', contactEmail: '', contactPhone: '' };
  protected athleteForm: CreateAthleteRequest & { id: string | null } = this.emptyAthlete();

  protected showTatamiForm = signal(false);
  protected showCategoryForm = signal(false);
  protected showCategoryGenerator = signal(false);
  protected categoryGeneratorStep = signal<GeneratorStep>('base');
  protected categoryGeneratorBusy = signal(false);
  protected categoryGeneratorPreview = signal<CategoryGenerationPreviewResponse | null>(null);
  protected categoryGeneratorApplyResult = signal<CategoryGenerationApplyResponse | null>(null);
  protected categoryGeneratorWarnings = signal<string[]>([]);
  protected showClubForm = signal(false);
  protected showAthleteForm = signal(false);

  protected categoryGeneratorForm: {
    minBirthYear: number | null;
    maxBirthYear: number | null;
    genderMode: CategoryGenerationGenderMode;
    matchDurationSeconds: number;
    goldenScoreEnabled: boolean;
    goldenScoreDurationSeconds: number;
    weightMode: CategoryGenerationWeightMode;
    groupSettings: GenerationGroupSettingForm[];
  } = {
    minBirthYear: null,
    maxBirthYear: null,
    genderMode: 'Male',
    matchDurationSeconds: 240,
    goldenScoreEnabled: false,
    goldenScoreDurationSeconds: 180,
    weightMode: 'StandardClasses',
    groupSettings: [],
  };

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

    if (tab !== 'athletes') {
      this.athleteImportInfo.set(null);
    }
  }

  protected genderLabel(g: Gender): string {
    if (g === 'Male') {
      return this.i18n.translate('gender.male');
    }

    if (g === 'Female') {
      return this.i18n.translate('gender.female');
    }

    return this.i18n.translate('gender.mixed');
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
    this.api.getRegistrations(id).subscribe({ next: (x) => this.registrations.set(x), error: this.onLoadError });
    this.api.getCategoryPresets(id).subscribe({ next: (x) => this.presets.set(x), error: this.onLoadError });
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
      next: () => {
        this.categories.update((list) => list.filter((x) => x.id !== c.id));
        this.api.getRegistrations(id).subscribe({ next: (x) => this.registrations.set(x) });
      },
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.delete'))),
    });
  }

  protected openCategoryGenerator(): void {
    if (!this.canOperate()) {
      return;
    }

    this.categoryGeneratorStep.set('base');
    this.categoryGeneratorBusy.set(false);
    this.categoryGeneratorPreview.set(null);
    this.categoryGeneratorApplyResult.set(null);
    this.categoryGeneratorWarnings.set([]);
    this.showCategoryGenerator.set(true);
    this.syncCategoryGeneratorGroupSettings();
  }

  protected closeCategoryGenerator(): void {
    this.showCategoryGenerator.set(false);
    this.categoryGeneratorBusy.set(false);
    this.categoryGeneratorStep.set('base');
    this.categoryGeneratorPreview.set(null);
    this.categoryGeneratorApplyResult.set(null);
    this.categoryGeneratorWarnings.set([]);
  }

  protected nextCategoryGeneratorStep(): void {
    if (this.categoryGeneratorStep() === 'base') {
      this.syncCategoryGeneratorGroupSettings();
      this.categoryGeneratorStep.set('strategy');
      return;
    }

    if (this.categoryGeneratorStep() === 'strategy') {
      this.previewGeneratedCategories();
    }
  }

  protected previousCategoryGeneratorStep(): void {
    if (this.categoryGeneratorStep() === 'preview') {
      this.categoryGeneratorStep.set('strategy');
      return;
    }

    if (this.categoryGeneratorStep() === 'strategy') {
      this.categoryGeneratorStep.set('base');
    }
  }

  protected syncCategoryGeneratorGroupSettings(): void {
    const keys = new Set(this.availableGenerationGroupKeys());
    const existing = new Map(
      this.categoryGeneratorForm.groupSettings.map((x) => [
        this.generationGroupKey(x.ageGroup, x.genderMode),
        x,
      ]),
    );

    const next: GenerationGroupSettingForm[] = [];
    for (const key of keys) {
      const current = existing.get(key);
      if (current) {
        next.push(current);
        continue;
      }

      const [ageGroup, genderModeRaw] = key.split('|');
      const genderMode = genderModeRaw as CategoryGenerationGenderMode;
      next.push({
        ageGroup,
        genderMode,
        targetAthletesPerCategory: 8,
        maxWeightDeviationKg: 2,
      });
    }

    this.categoryGeneratorForm.groupSettings = next.sort((a, b) =>
      a.ageGroup === b.ageGroup
        ? a.genderMode.localeCompare(b.genderMode)
        : a.ageGroup.localeCompare(b.ageGroup),
    );
  }

  protected previewGeneratedCategories(): void {
    if (!this.canOperate()) {
      return;
    }

    const id = this.tournamentId;
    if (!id) {
      return;
    }

    this.error.set(null);
    this.categoryGeneratorBusy.set(true);
    this.categoryGeneratorApplyResult.set(null);

    this.api.previewGeneratedCategories(id, this.buildGenerateCategoriesRequest()).subscribe({
      next: (preview) => {
        this.categoryGeneratorPreview.set(preview);
        this.categoryGeneratorWarnings.set(preview.warnings ?? []);
        this.categoryGeneratorStep.set('preview');
        this.categoryGeneratorBusy.set(false);
      },
      error: (err) => {
        this.categoryGeneratorBusy.set(false);
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
      },
    });
  }

  protected applyGeneratedCategories(): void {
    if (!this.canOperate()) {
      return;
    }

    const id = this.tournamentId;
    if (!id) {
      return;
    }

    this.error.set(null);
    this.categoryGeneratorBusy.set(true);

    this.api.applyGeneratedCategories(id, this.buildGenerateCategoriesRequest()).subscribe({
      next: (result) => {
        this.categoryGeneratorApplyResult.set(result);
        this.categoryGeneratorWarnings.set(result.warnings ?? []);
        this.categoryGeneratorBusy.set(false);
        this.showCategoryGenerator.set(false);
        this.api.getCategories(id).subscribe({ next: (x) => this.categories.set(x) });
        this.api.getRegistrations(id).subscribe({ next: (x) => this.registrations.set(x) });
      },
      error: (err) => {
        this.categoryGeneratorBusy.set(false);
        this.error.set(extractApiError(err, this.i18n.translate('errors.save')));
      },
    });
  }

  protected generatedWeightLabel(category: GeneratedCategoryProposal): string {
    return category.weightClassKg !== null
      ? `-${category.weightClassKg} kg`
      : this.i18n.translate('categories.weightOpen');
  }

  protected generatorGenderLabel(genderMode: CategoryGenerationGenderMode): string {
    if (genderMode === 'Male') {
      return this.i18n.translate('gender.male');
    }

    if (genderMode === 'Female') {
      return this.i18n.translate('gender.female');
    }

    return this.i18n.translate('gender.mixed');
  }

  // --- Clubs -------------------------------------------------------------
  protected newClub(): void {
    if (!this.canOperate()) {
      return;
    }
    this.clubForm = { id: null, name: '', contactName: '', contactEmail: '', contactPhone: '' };
    this.showClubForm.set(true);
  }

  protected editClub(c: Club): void {
    if (!this.canOperate()) {
      return;
    }
    this.clubForm = { id: c.id, name: c.name, contactName: c.contactName ?? '', contactEmail: c.contactEmail ?? '', contactPhone: c.contactPhone ?? '' };
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
    const contact = {
      contactName: f.contactName.trim() || null,
      contactEmail: f.contactEmail.trim() || null,
      contactPhone: f.contactPhone.trim() || null,
    };
    const req: Observable<unknown> = f.id
      ? this.api.updateClub(id, f.id, { name: f.name, ...contact })
      : this.api.createClub(id, { name: f.name, ...contact });
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

  protected openAthleteImportDialog(input: HTMLInputElement): void {
    if (!this.canOperate()) {
      return;
    }

    input.click();
  }

  protected importAthletesFromDm4(event: Event, allowDuplicate = false): void {
    if (!this.canOperate()) {
      return;
    }

    const id = this.tournamentId;
    if (!id) {
      return;
    }

    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    if (!file.name.toLowerCase().endsWith('.dm4')) {
      this.error.set(this.i18n.translate('athletes.importInvalidExtension'));
      this.athleteImportInfo.set(null);
      input.value = '';
      return;
    }

    this.error.set(null);
    this.athleteImportInfo.set(null);

    this.api.importAthletesFromDm4(id, file, allowDuplicate).subscribe({
      next: (created) => {
        this.athleteImportInfo.set(
          this.i18n.translate('athletes.importSuccess', { count: created.length }),
        );
        input.value = '';
        this.api.getAthletes(id).subscribe({ next: (x) => this.athletes.set(x) });
        this.api.getClubs(id).subscribe({ next: (x) => this.clubs.set(x) });
      },
      error: (err: unknown) => {
        if (this.isConflict(err)) {
          if (confirm(this.i18n.translate('athletes.importDuplicateConfirm'))) {
            this.importAthletesFromDm4(event, true);
            return;
          }

          this.error.set(null);
          input.value = '';
          return;
        }

        input.value = '';
        this.onSaveError(err);
      },
    });
  }

  private isConflict(err: unknown): boolean {
    return !!err && typeof err === 'object' && (err as { status?: number }).status === 409;
  }

  private buildGenerateCategoriesRequest(): GenerateCategoriesRequest {
    const groupSettings: CategoryGenerationGroupSetting[] =
      this.categoryGeneratorForm.weightMode === 'AthletesByTargetSize'
        ? this.categoryGeneratorForm.groupSettings.map((x) => ({
            ageGroup: x.ageGroup,
            genderMode: x.genderMode,
            targetAthletesPerCategory: Number(x.targetAthletesPerCategory),
            maxWeightDeviationKg: Number(x.maxWeightDeviationKg),
          }))
        : [];

    return {
      minBirthYear:
        this.categoryGeneratorForm.minBirthYear === null
        || (this.categoryGeneratorForm.minBirthYear as unknown) === ''
          ? null
          : Number(this.categoryGeneratorForm.minBirthYear),
      maxBirthYear:
        this.categoryGeneratorForm.maxBirthYear === null
        || (this.categoryGeneratorForm.maxBirthYear as unknown) === ''
          ? null
          : Number(this.categoryGeneratorForm.maxBirthYear),
      genderMode: this.categoryGeneratorForm.genderMode,
      matchDurationSeconds: Number(this.categoryGeneratorForm.matchDurationSeconds),
      goldenScoreEnabled: this.categoryGeneratorForm.goldenScoreEnabled,
      goldenScoreDurationSeconds: Number(this.categoryGeneratorForm.goldenScoreDurationSeconds),
      weightMode: this.categoryGeneratorForm.weightMode,
      groupSettings,
    };
  }

  private availableGenerationGroupKeys(): string[] {
    const ranges = GENERATION_AGE_GROUP_RANGES.filter((x) =>
      this.overlapsYearRange(
        x.minBirthYear,
        x.maxBirthYear,
        this.categoryGeneratorForm.minBirthYear,
        this.categoryGeneratorForm.maxBirthYear,
      ),
    );

    const keys: string[] = [];
    for (const range of ranges) {
      if (this.categoryGeneratorForm.genderMode === 'Mixed') {
        keys.push(this.generationGroupKey(range.ageGroup, 'Mixed'));
        continue;
      }

      keys.push(this.generationGroupKey(range.ageGroup, this.categoryGeneratorForm.genderMode));
    }

    return keys;
  }

  private overlapsYearRange(
    aMin: number | null,
    aMax: number | null,
    bMin: number | null,
    bMax: number | null,
  ): boolean {
    if (bMin !== null && bMax !== null) {
      return aMin !== null && aMax !== null && aMin <= bMin && aMax >= bMax;
    }

    const left = Math.max(aMin ?? Number.MIN_SAFE_INTEGER, bMin ?? Number.MIN_SAFE_INTEGER);
    const right = Math.min(aMax ?? Number.MAX_SAFE_INTEGER, bMax ?? Number.MAX_SAFE_INTEGER);
    return left <= right;
  }

  private generationGroupKey(ageGroup: string, genderMode: CategoryGenerationGenderMode): string {
    return `${ageGroup}|${genderMode}`;
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

  // --- Presets -----------------------------------------------------------
  protected presetWeightsLabel(preset: CategoryPreset): string {
    const limits = preset.weightClassLimitsKg;
    if (limits.length === 0) {
      return '-';
    }

    return limits
      .map((w) => (w === null ? '+' : String(w)))
      .join(', ');
  }

  protected presetBirthYearLabel(preset: CategoryPreset): string {
    const min = preset.minBirthYear;
    const max = preset.maxBirthYear;
    if (min == null && max == null) {
      return '-';
    }

    if (min != null && max == null) {
      return `≥ ${min}`;
    }

    if (min == null && max != null) {
      return `≤ ${max}`;
    }

    return `${min} – ${max}`;
  }

  protected addPresetRow(): void {
    if (!this.canOperate()) {
      return;
    }

    const current = this.presets();
    const newPreset: CategoryPreset = {
      id: '',
      ageGroup: '',
      gender: 'Male',
      maxAgeYears: null,
      minAgeYears: null,
      minBirthYear: null,
      maxBirthYear: null,
      defaultMatchDurationSeconds: 240,
      weightClassLimitsKg: [],
      sortOrder: current.length,
    };
    this.presets.set([...current, newPreset]);
    this.presetsDirty = true;
  }

  protected removePresetRow(index: number): void {
    if (!this.canOperate()) {
      return;
    }

    this.presets.update((list) => list.filter((_, i) => i !== index));
    this.presetsDirty = true;
  }

  protected onPresetChange(): void {
    this.presetsDirty = true;
  }

  protected savePresets(): void {
    if (!this.canOperate()) {
      return;
    }

    const id = this.tournamentId;
    if (!id) {
      return;
    }

    this.error.set(null);
    const items: CategoryPresetItemRequest[] = this.presets().map((p) => ({
      ageGroup: p.ageGroup,
      gender: p.gender,
      maxAgeYears: p.maxAgeYears === null || (p.maxAgeYears as unknown) === '' ? null : Number(p.maxAgeYears),
      minAgeYears: p.minAgeYears === null || (p.minAgeYears as unknown) === '' ? null : Number(p.minAgeYears),
      defaultMatchDurationSeconds: Number(p.defaultMatchDurationSeconds),
      weightClassLimitsKg: p.weightClassLimitsKg,
    }));

    this.api.updateCategoryPresets(id, items).subscribe({
      next: (saved) => {
        this.presets.set(saved);
        this.presetsDirty = false;
      },
      error: this.onSaveError,
    });
  }

  protected resetPresets(): void {
    if (!this.canOperate()) {
      return;
    }

    const id = this.tournamentId;
    if (!id || !confirm(this.i18n.translate('presets.confirmReset'))) {
      return;
    }

    this.error.set(null);
    this.api.resetCategoryPresetsToDefaults(id).subscribe({
      next: (saved) => {
        this.presets.set(saved);
        this.presetsDirty = false;
      },
      error: this.onSaveError,
    });
  }

  protected parseWeightLimitsInput(preset: CategoryPreset, raw: string): void {
    const parts = raw.split(',').map((s) => s.trim());
    const limits: (number | null)[] = parts.map((p) => {
      if (p === '' || p === 'null' || p === '+') {
        return null;
      }

      const n = parseFloat(p);
      return isNaN(n) ? null : n;
    });

    preset.weightClassLimitsKg = limits;
    this.presetsDirty = true;
  }
}
