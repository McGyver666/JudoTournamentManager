import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TournamentContextService } from '../../core/tournament-context.service';
import { I18nService } from '../../core/i18n.service';
import { extractApiError } from '../../core/http-error';
import { Athlete, Category, Gender, RegistrationDetail } from '../../core/models';
import { QrLicenseScannerComponent } from './qr-license-scanner.component';

/**
 * Registration management for the active tournament: register athletes to
 * categories, remove registrations and download the registration list as CSV.
 */
@Component({
  selector: 'app-registrations',
  standalone: true,
  imports: [FormsModule, TranslatePipe, QrLicenseScannerComponent],
  templateUrl: './registrations.component.html',
})
export class RegistrationsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);
  protected readonly canOperate = this.auth.canOperate;

  protected readonly registrations = signal<RegistrationDetail[]>([]);
  protected readonly athletes = signal<Athlete[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showForm = signal(false);
  protected readonly showQrScanner = signal(false);

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
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
    this.api.getAthletes(id).subscribe({ next: (x) => this.athletes.set(x) });
    this.api.getCategories(id).subscribe({ next: (x) => this.categories.set(x) });
  }

  protected startCreate(): void {
    if (!this.canOperate()) {
      return;
    }
    const firstAthlete = this.availableAthletes()[0];
    this.form = {
      athleteId: firstAthlete?.id ?? '',
      weightKg: firstAthlete?.weightKg ?? 0,
      licenseId: firstAthlete?.licenseId ?? '',
      licenseConfirmed: true,
      dokumeQrUrl: '',
      licenseCheckOverrideReason: ''
    };
    this.showForm.set(true);
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

  protected onAthleteSelected(): void {
    const selected = this.athletes().find((a) => a.id === this.form.athleteId);
    if (selected) {
      this.form.weightKg = selected.weightKg ?? 0;
      this.form.licenseId = selected.licenseId ?? '';
      // Preserve QR URL and override reason across athlete selection
    }
  }

  protected save(): void {
    if (!this.canOperate()) {
      return;
    }
    const id = this.tournamentId;
    if (!id) {
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
            licenseId: this.form.licenseId,
            weightKg: this.form.weightKg,
            grade: selected!.grade,
          };
          this.api.updateAthlete(id, selected!.id, updateRequest).subscribe({
            next: () => {
              this.showForm.set(false);
              this.load();
            },
            error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.save'))),
          });
        } else {
          this.showForm.set(false);
          this.load();
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

  protected exportCsv(): void {
    const id = this.tournamentId;
    if (!id) {
      return;
    }
    // Direct navigation triggers the browser's download of the CSV file.
    window.location.href = this.api.registrationsExportUrl(id);
  }

  protected weightLabel(kg: number | null): string {
    return kg !== null ? `-${kg} kg` : this.i18n.translate('categories.weightOpen');
  }

  /**
   * Checks if the registration form is valid for submission.
   */
  protected isFormValid(): boolean {
    return !!this.form.weightKg && this.form.licenseConfirmed;
  }
}
