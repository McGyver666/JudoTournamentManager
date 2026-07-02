import { Injectable, computed, signal } from '@angular/core';
import { ApiService } from './api.service';
import { extractApiError } from './http-error';
import { AuthenticatedUser } from './models';

const TOKEN_KEY = 'judo.auth.token';
const EXPIRES_KEY = 'judo.auth.expires';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly tokenValue = signal<string | null>(null);

  readonly user = signal<AuthenticatedUser | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly isAuthenticated = computed(() => this.user() !== null && this.tokenValue() !== null);
  readonly role = computed(() => this.user()?.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');
  readonly canOperate = computed(() => this.role() === 'Admin' || this.role() === 'Operator');
  readonly canDisplay = computed(() => this.canOperate() || this.role() === 'Display');

  constructor(private readonly api: ApiService) {
    this.restoreToken();
  }

  token(): string | null {
    return this.tokenValue();
  }

  init(): Promise<void> {
    const token = this.tokenValue();
    if (!token) {
      return Promise.resolve();
    }

    this.loading.set(true);
    this.error.set(null);

    return new Promise((resolve) => {
      this.api.me().subscribe({
        next: (user) => {
          this.user.set({ userId: user.userId, userName: user.userName, role: user.role as 'Admin' | 'Operator' | 'Display' });
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
          this.tokenValue.set(response.accessToken);
          this.user.set({
            userId: '',
            userName: response.userName,
            role: response.role,
          });
          this.persistToken(response.accessToken, response.expiresAtUtc);

          this.api.me().subscribe({
            next: (me) => {
              this.user.set({ userId: me.userId, userName: me.userName, role: me.role as 'Admin' | 'Operator' | 'Display' });
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

  private restoreToken(): void {
    try {
      const token = localStorage.getItem(TOKEN_KEY);
      const expires = localStorage.getItem(EXPIRES_KEY);
      if (!token || !expires) {
        return;
      }

      if (new Date(expires).getTime() <= Date.now()) {
        this.clearSession();
        return;
      }

      this.tokenValue.set(token);
    } catch {
      // Ignore storage access errors.
    }
  }

  private persistToken(token: string, expiresAtUtc: string): void {
    try {
      localStorage.setItem(TOKEN_KEY, token);
      localStorage.setItem(EXPIRES_KEY, expiresAtUtc);
    } catch {
      // Ignore storage access errors.
    }
  }

  private clearSession(): void {
    this.tokenValue.set(null);
    this.user.set(null);
    try {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(EXPIRES_KEY);
    } catch {
      // Ignore storage access errors.
    }
  }
}
