import { Injectable, computed, signal } from '@angular/core';
import { ApiService } from './api.service';
import { extractApiError } from './http-error';
import { AuthenticatedUser, UserRole } from './models';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  /**
   * Ephemeral bearer token supplied via a guest share link (URL query param).
   * Never persisted to storage and takes precedence over any login token so the
   * public match-lists page and its SignalR/API calls authenticate as a guest.
   */
  private readonly guestTokenValue = signal<string | null>(null);

  readonly user = signal<AuthenticatedUser | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly role = computed(() => this.user()?.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');
  readonly canOperate = computed(() => this.role() === 'Admin' || this.role() === 'Operator');
  readonly canOperateLive = computed(() => this.canOperate() || this.role() === 'Competition');
  readonly canDisplay = computed(() => this.canOperateLive() || this.role() === 'Display');

  constructor(private readonly api: ApiService) {}

  token(): string | null {
    return this.guestTokenValue();
  }

  /** Sets the ephemeral guest token used by the public match-lists page. */
  setGuestToken(token: string | null): void {
    this.guestTokenValue.set(token && token.length > 0 ? token : null);
  }

  init(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    return new Promise((resolve) => {
      this.api.me().subscribe({
        next: (user) => {
          this.user.set({ userId: user.userId, userName: user.userName, role: user.role as UserRole });
          this.loading.set(false);
          resolve();
        },
        error: () => {
          this.clearSession();
          this.loading.set(false);
          resolve();
        },
      });
    });
  }

  login(userName: string, password: string): Promise<boolean> {
    this.loading.set(true);
    this.error.set(null);

    return new Promise((resolve) => {
      this.api.login({ userName, password }).subscribe({
        next: (response) => {
          this.user.set({
            userId: '',
            userName: response.userName,
            role: response.role,
          });

          this.api.me().subscribe({
            next: (me) => {
              this.user.set({ userId: me.userId, userName: me.userName, role: me.role as UserRole });
              this.loading.set(false);
              resolve(true);
            },
            error: () => {
              this.loading.set(false);
              resolve(true);
            },
          });
        },
        error: (err) => {
          this.error.set(extractApiError(err, 'Anmeldung fehlgeschlagen.'));
          this.clearSession();
          this.loading.set(false);
          resolve(false);
        },
      });
    });
  }

  logout(): Promise<void> {
    return new Promise((resolve) => {
      this.api.logout().subscribe({
        next: () => {
          this.clearSession();
          resolve();
        },
        error: () => {
          this.clearSession();
          resolve();
        },
      });
    });
  }

  private clearSession(): void {
    this.user.set(null);
  }
}
