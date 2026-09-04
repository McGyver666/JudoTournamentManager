import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TournamentContextService } from '../../core/tournament-context.service';
import { I18nService } from '../../core/i18n.service';
import { extractApiError } from '../../core/http-error';
import { Athlete, Category, Club, Gender, RegistrationDetail } from '../../core/models';
import { QrLicenseScannerComponent } from './qr-license-scanner.component';

/**
 * Registration management for the active tournament: register athletes to
 * categories and remove registrations.
 */
@Component({
  selector: 'app-registrations',
  standalone: true,
  imports: [FormsModule, TranslatePipe, QrLicenseScannerComponent],
  templateUrl: './registrations.component.html',
  styleUrl: './registrations.component.css',
})
export class RegistrationsComponent implements OnInit {
  private static readonly visibleAthleteRows = 6;

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);
  protected readonly canOperate = this.auth.canOperate;

  protected readonly registrations = signal<RegistrationDetail[]>([]);
  protected readonly athletes = signal<Athlete[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly clubs = signal<Club[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showQrScanner = signal(false);
  protected readonly searchQuery = signal('');
  protected readonly selectedClubId = signal('');
  protected readonly selectedAgeGroup = signal('');
  protected readonly selectedGender = signal<'All' | Gender>('All');
  protected readonly activeAthleteId = signal<string | null>(null);
  protected readonly toastMessage = signal<string | null>(null);

  @ViewChild('searchInput')
  private searchInput?: ElementRef<HTMLInputElement>;

  @ViewChild('weightInput')
  private weightInput?: ElementRef<HTMLInputElement>;

  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  protected form = {
    athleteId: '',
    weightKg: 0 as number,
    licenseId: '',
    licenseConfirmed: true,
    dokumeQrUrl: '',
    licenseCheckOverrideReason: ''
  };

  ngOnInit(): void {
    if (this.context.tournamentId()) {
      this.load();
    }
  }

  protected get tournamentId(): string | null {
    return this.context.tournamentId();
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

  protected categoryLabel(c: Category): string {
    const weight = c.weightClassKg !== null
      ? `-${c.weightClassKg} kg`
      : this.i18n.translate('categories.weightOpen');
    return `${c.name} (${c.ageGroup}, ${this.genderLabel(c.gender)}, ${weight})`;
  }

  protected athleteLabel(a: Athlete): string {
    return `${a.lastName}, ${a.firstName} (${a.birthYear})`;
  }

  /**
   * Returns athletes not yet registered in this tournament.
   */
  protected availableAthletes(): Athlete[] {
    const registeredIds = new Set(this.registrations().map((r) => r.athleteId));
    return this.athletes().filter((a) => !registeredIds.has(a.id));
  }

  protected load(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.getRegistrations(id).subscribe({
      next: (x) => {
        this.registrations.set(x);
        this.syncActiveAthlete();
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
    this.api.getAthletes(id).subscribe({
      next: (x) => {
        this.athletes.set(x);
        this.syncActiveAthlete();
      }
    });
    this.api.getCategories(id).subscribe({
      next: (x) => {
        this.categories.set(x);
        this.syncActiveAthlete();
      }
    });
    this.api.getClubs(id).subscribe({ next: (x) => this.clubs.set(x) });
  }

  protected onQrScanned(event: { qrUrl: string; passNumber: string | null }): void {
    this.form.dokumeQrUrl = event.qrUrl;
    if (event.passNumber) {
      this.form.licenseId = event.passNumber;
    }
    this.showQrScanner.set(false);
  }

  protected onScanCancelled(): void {
    this.showQrScanner.set(false);
  }

  protected onSearchChanged(value: string): void {
    this.searchQuery.set(value);
    this.syncActiveAthlete();
  }

  protected onFilterClubChanged(value: string): void {
    this.selectedClubId.set(value);
    this.syncActiveAthlete();
  }

  protected onFilterAgeGroupChanged(value: string): void {
    this.selectedAgeGroup.set(value);
    this.syncActiveAthlete();
  }

  protected onFilterGenderChanged(value: 'All' | Gender): void {
    this.selectedGender.set(value);
    this.syncActiveAthlete();
  }

  protected resetFilters(): void {
    this.selectedClubId.set('');
    this.selectedAgeGroup.set('');
    this.selectedGender.set('All');
    this.syncActiveAthlete();
  }

  protected filteredAthletes(): Athlete[] {
    const registeredIds = new Set(this.registrations().map((r) => r.athleteId));
    const query = this.searchQuery().trim().toLowerCase();
    const clubId = this.selectedClubId();
    const ageGroup = this.selectedAgeGroup();
    const gender = this.selectedGender();

    return this.athletes()
      .filter((a) => !registeredIds.has(a.id))
      .filter((a) => !clubId || a.clubId === clubId)
      .filter((a) => gender === 'All' || a.gender === gender)
      .filter((a) => !ageGroup || this.matchesAgeGroupFilter(a, ageGroup))
      .filter((a) => {
        if (!query) {
          return true;
        }

        const firstLast = `${a.firstName} ${a.lastName}`.toLowerCase();
        const lastFirst = `${a.lastName} ${a.firstName}`.toLowerCase();
        return firstLast.includes(query) || lastFirst.includes(query);
      })
      .sort((left, right) => {
        const lastName = left.lastName.localeCompare(right.lastName, 'de');
        if (lastName !== 0) {
          return lastName;
        }

        return left.firstName.localeCompare(right.firstName, 'de');
      });
  }

  protected onSearchKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.moveActiveBy(1);
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.moveActiveBy(-1);
      return;
    }

    if (event.key === 'Enter') {
      event.preventDefault();
      if (this.activeAthleteId()) {
        this.focusWeightInput(true);
      }
    }
  }

  protected onWeightKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      if (this.isFormValid()) {
        this.save();
      }
    }
  }

  protected selectAthlete(athleteId: string, moveFocusToWeight: boolean): void {
    const selected = this.athletes().find((a) => a.id === athleteId);
    if (!selected) {
      return;
    }

    this.activeAthleteId.set(selected.id);
    this.form.athleteId = selected.id;
    this.form.weightKg = selected.weightKg ?? 0;
    this.form.licenseId = selected.licenseId ?? '';

    if (moveFocusToWeight) {
      this.focusWeightInput(true);
    }
  }

  protected visibleAthleteCountHint(): number {
    return this.filteredAthletes().length;
  }

  protected hasNoMatches(): boolean {
    return this.filteredAthletes().length === 0;
  }

  protected hasMoreThanVisibleRows(): boolean {
    return this.filteredAthletes().length > RegistrationsComponent.visibleAthleteRows;
  }

  protected clubOptions(): Club[] {
    return [...this.clubs()].sort((left, right) => left.name.localeCompare(right.name, 'de'));
  }

  protected ageGroupOptions(): string[] {
    return Array.from(new Set(this.categories().map((c) => c.ageGroup)))
      .sort((left, right) => left.localeCompare(right, 'de'));
  }

  protected activeAthleteLabel(): string {
    const id = this.activeAthleteId();
    if (!id) {
      return this.i18n.translate('registrations.selectAthleteFirst');
    }

    const selected = this.athletes().find((a) => a.id === id);
    if (!selected) {
      return this.i18n.translate('registrations.selectAthleteFirst');
    }

    return `${selected.firstName} ${selected.lastName} (${selected.birthYear})`;
  }

  protected athleteListLabel(a: Athlete): string {
    return `${a.firstName} ${a.lastName} (${a.birthYear})`;
  }

  protected isActiveAthlete(athleteId: string): boolean {
    return this.activeAthleteId() === athleteId;
  }

  protected save(): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    if (!this.form.athleteId) {
      this.error.set(this.i18n.translate('registrations.selectAthleteFirst'));
      return;
    }
    if (!this.form.weightKg) {
      this.error.set(this.i18n.translate('errors.weightRequired'));
      return;
    }
    if (!this.form.licenseConfirmed) {
      this.error.set(this.i18n.translate('errors.licenseConfirmationRequired'));
      return;
    }
    this.error.set(null);

    const request = {
      athleteId: this.form.athleteId,
      weightKg: this.form.weightKg,
      licenseId: this.form.licenseId || null,
      licenseConfirmed: this.form.licenseConfirmed,
      dokumeQrUrl: this.form.dokumeQrUrl || undefined,
      licenseCheckOverrideReason: this.form.licenseCheckOverrideReason || undefined
    };

    this.api.createRegistration(id, request).subscribe({
      next: () => {
        // Update athlete if weight or license changed during registration
        const selected = this.athletes().find((a) => a.id === this.form.athleteId);
        const weightChanged = selected && this.form.weightKg && selected.weightKg !== this.form.weightKg;
        const licenseChanged = selected && this.form.licenseId && selected.licenseId !== this.form.licenseId;

        if (weightChanged || licenseChanged) {
          const updateRequest = {
            clubId: selected!.clubId,
            firstName: selected!.firstName,
            lastName: selected!.lastName,
            birthYear: selected!.birthYear,
            gender: selected!.gender,
            licenseId: selected!.licenseId,
            weightKg: this.form.weightKg,
            grade: selected!.grade,
          };
          this.api.updateAthlete(id, selected!.id, updateRequest).subscribe({
            next: () => {
              this.afterSuccessfulSave();
            },
            error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.save'))),
          });
        } else {
          this.afterSuccessfulSave();
        }
      },
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.save'))),
    });
  }

  protected remove(r: RegistrationDetail): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id || !confirm(this.i18n.translate('registrations.confirmDelete'))) {
      return;
    }
    this.api.deleteRegistration(id, r.id).subscribe({
      next: () => this.registrations.update((list) => list.filter((x) => x.id !== r.id)),
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.delete'))),
    });
  }

  protected weightLabel(kg: number | null): string {
    return kg !== null ? `-${kg} kg` : this.i18n.translate('categories.weightOpen');
  }

  /**
   * Checks if the registration form is valid for submission.
   */
  protected isFormValid(): boolean {
    return !!this.form.athleteId && !!this.form.weightKg && this.form.licenseConfirmed;
  }

  protected clearToast(): void {
    this.toastMessage.set(null);
  }

  private moveActiveBy(step: number): void {
    const athletes = this.filteredAthletes();
    if (athletes.length === 0) {
      return;
    }

    const currentIndex = Math.max(
      athletes.findIndex((a) => a.id === this.activeAthleteId()),
      0,
    );
    const nextIndex = (currentIndex + step + athletes.length) % athletes.length;
    this.selectAthlete(athletes[nextIndex].id, false);
  }

  private syncActiveAthlete(): void {
    const athletes = this.filteredAthletes();
    if (athletes.length === 0) {
      this.activeAthleteId.set(null);
      this.form.athleteId = '';
      return;
    }

    const activeId = this.activeAthleteId();
    if (activeId && athletes.some((a) => a.id === activeId)) {
      this.selectAthlete(activeId, false);
      return;
    }

    this.selectAthlete(athletes[0].id, false);
  }

  private matchesAgeGroupFilter(athlete: Athlete, ageGroup: string): boolean {
    const categoriesInGroup = this.categories().filter((c) => c.ageGroup === ageGroup);
    if (categoriesInGroup.length === 0) {
      return false;
    }

    return categoriesInGroup.some((category) => {
      const minOk = category.minBirthYear === null || athlete.birthYear >= category.minBirthYear;
      const maxOk = category.maxBirthYear === null || athlete.birthYear <= category.maxBirthYear;
      const genderOk = category.gender === 'Mixed' || category.gender === athlete.gender;
      return minOk && maxOk && genderOk;
    });
  }

  private afterSuccessfulSave(): void {
    this.error.set(null);
    this.searchQuery.set('');
    this.form.dokumeQrUrl = '';
    this.form.licenseCheckOverrideReason = '';
    this.showQrScanner.set(false);
    this.showToast(this.i18n.translate('registrations.savedToast'));
    this.focusSearchInput();
    this.load();
  }

  private showToast(message: string): void {
    this.toastMessage.set(message);
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this.toastTimeoutId = setTimeout(() => {
      this.toastMessage.set(null);
      this.toastTimeoutId = null;
    }, 1800);
  }

  private focusSearchInput(): void {
    setTimeout(() => {
      this.searchInput?.nativeElement.focus();
      this.searchInput?.nativeElement.select();
    });
  }

  private focusWeightInput(selectAll: boolean): void {
    setTimeout(() => {
      const input = this.weightInput?.nativeElement;
      if (!input) {
        return;
      }

      input.focus();
      if (selectAll) {
        input.select();
      }
    });
  }
}
