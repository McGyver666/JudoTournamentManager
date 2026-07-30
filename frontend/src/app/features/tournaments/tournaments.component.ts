import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TournamentContextService } from '../../core/tournament-context.service';
import { I18nService } from '../../core/i18n.service';
import { extractApiError } from '../../core/http-error';
import { AccentSideColor, CreateTournamentRequest, GuestShareResponse, Tournament } from '../../core/models';

/** Auto-off presets offered when enabling or rotating a guest share link. */
type GuestShareTtl = 'midnight' | '4h' | '8h' | 'none';

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
  private readonly sanitizer = inject(DomSanitizer);
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

  // Guest share (QR) management -------------------------------------------
  protected readonly shareTournamentId = signal<string | null>(null);
  protected readonly shareState = signal<GuestShareResponse | null>(null);
  protected readonly shareQr = signal<SafeHtml | null>(null);
  protected readonly shareBusy = signal(false);
  protected readonly shareError = signal<string | null>(null);
  protected readonly shareTtlOptions: GuestShareTtl[] = ['midnight', '4h', '8h', 'none'];
  protected shareTtl: GuestShareTtl = 'midnight';

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
      minimumRestBetweenFightsSeconds: t.minimumRestBetweenFightsSeconds,
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

  // Guest share (QR) management -------------------------------------------

  protected isShareOpen(t: Tournament): boolean {
    return this.shareTournamentId() === t.id;
  }

  protected toggleShare(t: Tournament): void {
    if (!this.canManage()) {
      return;
    }
    if (this.shareTournamentId() === t.id) {
      this.closeShare();
      return;
    }

    this.shareTournamentId.set(t.id);
    this.shareState.set(null);
    this.shareQr.set(null);
    this.shareError.set(null);
    this.shareTtl = 'midnight';
    this.loadShareState(t.id);
  }

  protected closeShare(): void {
    this.shareTournamentId.set(null);
    this.shareState.set(null);
    this.shareQr.set(null);
    this.shareError.set(null);
  }

  protected enableShare(): void {
    const tid = this.shareTournamentId();
    if (!tid || !this.canManage()) {
      return;
    }
    this.runShareAction(this.api.enableGuestShare(tid, this.computeExpiry()));
  }

  protected rotateShare(): void {
    const tid = this.shareTournamentId();
    if (!tid || !this.canManage()) {
      return;
    }
    if (!confirm(this.i18n.translate('share.confirmRotate'))) {
      return;
    }
    this.runShareAction(this.api.rotateGuestShare(tid, this.computeExpiry()));
  }

  protected disableShare(): void {
    const tid = this.shareTournamentId();
    if (!tid || !this.canManage()) {
      return;
    }
    this.runShareAction(this.api.disableGuestShare(tid));
  }

  protected copyShareUrl(): void {
    const url = this.shareState()?.publicUrl;
    if (!url) {
      return;
    }
    void navigator.clipboard?.writeText(url).then(
      () => this.info.set(this.i18n.translate('share.copied')),
      () => undefined,
    );
  }

  private loadShareState(tid: string): void {
    this.shareBusy.set(true);
    this.shareError.set(null);
    this.api.getGuestShare(tid).subscribe({
      next: (state) => {
        this.shareState.set(state);
        this.shareBusy.set(false);
        this.loadShareQr(tid, state);
      },
      error: (err) => {
        this.shareError.set(extractApiError(err, this.i18n.translate('errors.load')));
        this.shareBusy.set(false);
      },
    });
  }

  private runShareAction(action: Observable<GuestShareResponse>): void {
    const tid = this.shareTournamentId();
    if (!tid) {
      return;
    }
    this.shareBusy.set(true);
    this.shareError.set(null);
    action.subscribe({
      next: (state) => {
        this.shareState.set(state);
        this.shareBusy.set(false);
        this.loadShareQr(tid, state);
      },
      error: (err) => {
        this.shareError.set(extractApiError(err, this.i18n.translate('errors.save')));
        this.shareBusy.set(false);
      },
    });
  }

  private loadShareQr(tid: string, state: GuestShareResponse): void {
    if (!state.exists) {
      this.shareQr.set(null);
      return;
    }
    this.api.getGuestShareQr(tid).subscribe({
      next: (svg) => this.shareQr.set(this.sanitizer.bypassSecurityTrustHtml(svg)),
      error: () => this.shareQr.set(null),
    });
  }

  /** Translates the selected auto-off preset into an absolute UTC expiry (or null). */
  private computeExpiry(): string | null {
    const now = new Date();
    switch (this.shareTtl) {
      case 'none':
        return null;
      case '4h':
        return new Date(now.getTime() + 4 * 60 * 60 * 1000).toISOString();
      case '8h':
        return new Date(now.getTime() + 8 * 60 * 60 * 1000).toISOString();
      case 'midnight':
      default: {
        const midnight = new Date(now);
        midnight.setHours(23, 59, 59, 999);
        return midnight.toISOString();
      }
    }
  }

  protected shareTtlLabelKey(ttl: GuestShareTtl): string {
    return `share.ttl.${ttl}`;
  }

  private emptyForm(): CreateTournamentRequest {
    return { name: '', date: '', venue: '', organizer: '', accentSideColor: 'Blue', osaeKomiIpponSeconds: 20, osaeKomiWazaAriSeconds: 10, osaeKomiYukoSeconds: 5, osaeKomiYukoEnabled: true, minimumRestBetweenFightsSeconds: 180 };
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
