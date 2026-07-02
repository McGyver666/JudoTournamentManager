import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { extractApiError } from '../../core/http-error';
import { CreateUserRequest, LocalUserAccount, UserRole } from '../../core/models';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.css',
})
export class UserManagementComponent implements OnInit {
  private readonly api = inject(ApiService);
  private static readonly PASSWORD_MIN_LENGTH = 12;

  protected readonly users = signal<LocalUserAccount[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly info = signal<string | null>(null);
  protected readonly resetTarget = signal<LocalUserAccount | null>(null);
  protected readonly resetPasswordValue = signal('');

  protected readonly form = signal<CreateUserRequest>({
    userName: '',
    role: 'Operator',
    password: '',
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getUsers().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(extractApiError(err, 'Daten konnten nicht geladen werden.'));
        this.loading.set(false);
      },
    });
  }

  protected createUser(): void {
    const form = this.form();
    this.saving.set(true);
    this.error.set(null);
    this.info.set(null);
    this.api.createUser(form).subscribe({
      next: () => {
        this.info.set('Benutzer wurde erstellt.');
        this.form.set({ userName: '', role: 'Operator', password: '' });
        this.saving.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(extractApiError(err, 'Benutzer konnte nicht erstellt werden.'));
        this.saving.set(false);
      },
    });
  }

  protected toggleActive(user: LocalUserAccount): void {
    this.error.set(null);
    this.info.set(null);
    this.api.setUserActive(user.id, { isActive: !user.isActive }).subscribe({
      next: () => {
        this.info.set('Benutzerstatus wurde aktualisiert.');
        this.load();
      },
      error: (err) => this.error.set(extractApiError(err, 'Status konnte nicht aktualisiert werden.')),
    });
  }

  protected openResetPassword(user: LocalUserAccount): void {
    this.resetTarget.set(user);
    this.resetPasswordValue.set('');
    this.error.set(null);
    this.info.set(null);
  }

  protected closeResetPassword(): void {
    this.resetTarget.set(null);
    this.resetPasswordValue.set('');
  }

  protected confirmResetPassword(): void {
    const user = this.resetTarget();
    if (!user) {
      return;
    }

    const newPassword = this.resetPasswordValue().trim();
    if (newPassword.length < UserManagementComponent.PASSWORD_MIN_LENGTH) {
      this.error.set('Das Passwort muss mindestens 12 Zeichen lang sein.');
      return;
    }

    this.error.set(null);
    this.info.set(null);
    this.api.resetUserPassword(user.id, { newPassword }).subscribe({
      next: () => {
        this.info.set('Passwort wurde zurückgesetzt.');
        this.closeResetPassword();
      },
      error: (err) => this.error.set(extractApiError(err, 'Passwort konnte nicht zurückgesetzt werden.')),
    });
  }

  protected updateUserName(value: string): void {
    this.form.update((x) => ({ ...x, userName: value }));
  }

  protected updatePassword(value: string): void {
    this.form.update((x) => ({ ...x, password: value }));
  }

  protected updateRole(value: string): void {
    this.form.update((x) => ({ ...x, role: value as UserRole }));
  }

  protected updateResetPassword(value: string): void {
    this.resetPasswordValue.set(value);
  }
}
