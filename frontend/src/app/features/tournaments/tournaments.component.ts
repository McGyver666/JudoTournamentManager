import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TournamentContextService } from '../../core/tournament-context.service';
import { I18nService } from '../../core/i18n.service';
import { extractApiError } from '../../core/http-error';
import { AccentSideColor, CreateTournamentRequest, Tournament } from '../../core/models';

/** Tournament administration: list, create, edit, delete and select the active tournament. */
@Component({
  selector: 'app-tournaments',
  standalone: true,
  imports: [FormsModule, DatePipe, TranslatePipe],
  templateUrl: './tournaments.component.html',
})
export class TournamentsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);

  protected readonly tournaments = signal<Tournament[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly info = signal<string | null>(null);
  protected readonly showForm = signal(false);
  protected readonly editId = signal<string | null>(null);
  protected readonly backupBusyId = signal<string | null>(null);
  protected readonly restoring = signal(false);
  protected readonly canManage = this.auth.canOperate;
  protected readonly isAdmin = this.auth.isAdmin;
  protected readonly accentSideColors: AccentSideColor[] = ['Blue', 'Red'];

  protected form: CreateTournamentRequest = this.emptyForm();

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getTournaments().subscribe({
      next: (list) => {
        this.tournaments.set(list);
        const activeTournamentId = this.context.tournamentId();
        if (activeTournamentId) {
          const activeTournament = list.find((t) => t.id === activeTournamentId);
          if (activeTournament) {
            this.context.refreshIfActive(activeTournament);
          }
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
  }

  protected startCreate(): void {
    if (!this.canManage()) {
      return;
    }
    this.info.set(null);
    this.editId.set(null);
    this.form = this.emptyForm();
    this.showForm.set(true);
  }

  protected startEdit(t: Tournament): void {
    if (!this.canManage()) {
      return;
    }
    this.info.set(null);
    this.editId.set(t.id);
    this.form = {
      name: t.name,
      date: t.date,
      venue: t.venue,
      organizer: t.organizer,
      accentSideColor: t.accentSideColor,
      osaeKomiIpponSeconds: t.osaeKomiIpponSeconds,
      osaeKomiWazaAriSeconds: t.osaeKomiWazaAriSeconds,
      osaeKomiYukoSeconds: t.osaeKomiYukoSeconds,
      osaeKomiYukoEnabled: t.osaeKomiYukoEnabled,
    };
    this.showForm.set(true);
  }

  protected cancel(): void {
    this.showForm.set(false);
    this.error.set(null);
  }

  protected save(): void {
    if (!this.canManage()) {
      return;
    }
    this.error.set(null);
    this.info.set(null);
    const id = this.editId();
    const request: Observable<unknown> = id
      ? this.api.updateTournament(id, this.form)
      : this.api.createTournament(this.form);
    request.subscribe({
      next: () => {
        this.showForm.set(false);
        this.load();
      },
      error: (err: unknown) => this.error.set(extractApiError(err, this.i18n.translate('errors.save'))),
    });
  }

  protected remove(t: Tournament): void {
    if (!this.canManage()) {
      return;
    }
    if (!confirm(this.i18n.translate('tournaments.confirmDelete'))) {
      return;
    }
    this.api.deleteTournament(t.id).subscribe({
      next: () => {
        if (this.context.tournamentId() === t.id) {
          this.context.clear();
        }
        this.load();
      },
      error: (err) => this.error.set(extractApiError(err, this.i18n.translate('errors.delete'))),
    });
  }

  protected select(t: Tournament): void {
    this.info.set(null);
    this.context.select(t);
  }

  protected backup(t: Tournament): void {
    if (!this.isAdmin()) {
      return;
    }

    this.error.set(null);
    this.info.set(null);
    this.backupBusyId.set(t.id);

    this.api.downloadTournamentBackup(t.id).subscribe({
      next: (response) => {
        this.saveBackupFile(t, response);
        this.info.set(this.i18n.translate('tournaments.backupDone'));
        this.backupBusyId.set(null);
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('tournaments.backupFailed')));
        this.backupBusyId.set(null);
      },
    });
  }

  protected openRestoreDialog(input: HTMLInputElement): void {
    if (!this.isAdmin()) {
      return;
    }

    input.click();
  }

  protected async restoreFromFile(event: Event): Promise<void> {
    if (!this.isAdmin()) {
      return;
    }

    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.error.set(null);
    this.info.set(null);
    this.restoring.set(true);

    let payload: unknown;
    try {
      const raw = await file.text();
      payload = JSON.parse(raw) as unknown;
    } catch {
      this.error.set(this.i18n.translate('tournaments.restoreInvalidFile'));
      this.restoring.set(false);
      input.value = '';
      return;
    }

    this.api.restoreTournamentBackup(payload).subscribe({
      next: () => {
        this.info.set(this.i18n.translate('tournaments.restoreDone'));
        this.restoring.set(false);
        input.value = '';
        this.load();
      },
      error: (err) => {
        this.error.set(extractApiError(err, this.i18n.translate('tournaments.restoreFailed')));
        this.restoring.set(false);
        input.value = '';
      },
    });
  }

  protected isActive(t: Tournament): boolean {
    return this.context.tournamentId() === t.id;
  }

  protected isBackupBusy(tournamentId: string): boolean {
    return this.backupBusyId() === tournamentId;
  }

  private emptyForm(): CreateTournamentRequest {
    return { name: '', date: '', venue: '', organizer: '', accentSideColor: 'Blue', osaeKomiIpponSeconds: 20, osaeKomiWazaAriSeconds: 10, osaeKomiYukoSeconds: 5, osaeKomiYukoEnabled: true };
  }

  protected colorLabelKey(color: AccentSideColor): string {
    return `tournaments.${color.toLowerCase()}`;
  }

  private saveBackupFile(t: Tournament, response: HttpResponse<Blob>): void {
    const blob = response.body;
    if (!blob) {
      throw new Error('Backup response was empty.');
    }

    const safeName = t.name.replace(/[^a-zA-Z0-9_-]+/g, '-');
    const fileName = this.tryGetFileName(response) ?? `turnier-backup-${safeName || t.id}.json`;
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }

  private tryGetFileName(response: HttpResponse<Blob>): string | null {
    const contentDisposition = response.headers.get('content-disposition');
    if (!contentDisposition) {
      return null;
    }

    const match = /filename="?([^";]+)"?/i.exec(contentDisposition);
    return match?.[1] ?? null;
  }
}
