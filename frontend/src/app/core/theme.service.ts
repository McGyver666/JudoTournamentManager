import { Injectable, signal } from '@angular/core';

/**
 * Supported UI colour themes. Light is the default product theme; dark is the
 * "Dojo bei Abenddämmerung" variant defined via <c>[data-theme="dark"]</c>.
 */
export type AppTheme = 'light' | 'dark';

/** localStorage key holding the per-device theme choice. */
export const THEME_STORAGE_KEY = 'judo.theme';

const DEFAULT_THEME: AppTheme = 'light';

/**
 * Persists the operator's light/dark choice per device and keeps the
 * <c>data-theme</c> attribute on the document root in sync with it.
 *
 * An inline bootstrap in <c>index.html</c> applies the stored theme before the
 * first paint to avoid a flash of the wrong theme; this service is the runtime
 * source of truth once Angular has started and reconciles with that value.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  /** Active theme; exposed as a signal so views react to changes. */
  readonly theme = signal<AppTheme>(this.readStoredTheme() ?? DEFAULT_THEME);

  constructor() {
    this.applyDocumentTheme();
  }

  /** Activates a theme and persists the choice locally. */
  use(theme: AppTheme): void {
    this.theme.set(theme);
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // Storage may be unavailable (private mode); keep in-memory state.
    }
    this.applyDocumentTheme();
  }

  /** Flips between the light and dark token sets. */
  toggle(): void {
    this.use(this.theme() === 'dark' ? 'light' : 'dark');
  }

  private readStoredTheme(): AppTheme | null {
    try {
      const value = localStorage.getItem(THEME_STORAGE_KEY);
      return value === 'light' || value === 'dark' ? value : null;
    } catch {
      return null;
    }
  }

  private applyDocumentTheme(): void {
    document.documentElement.setAttribute('data-theme', this.theme());
  }
}
