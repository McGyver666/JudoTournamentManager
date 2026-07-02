import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * Supported UI languages. German is the default product language;
 * English is provided as a fallback / placeholder locale.
 */
export type AppLanguage = 'de' | 'en';

const STORAGE_KEY = 'judo.lang';
const DEFAULT_LANGUAGE: AppLanguage = 'de';

/**
 * Lightweight runtime translation service.
 *
 * Loads per-language dictionaries from <c>/i18n/{lang}.json</c> at startup and
 * resolves dotted translation keys with parameter interpolation. Keeping the
 * pipeline runtime-based (instead of build-time @angular/localize) keeps the
 * offline-first deployment simple: a single bundle serves every locale and the
 * JSON dictionaries are shipped as static assets.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly dictionaries = new Map<AppLanguage, Record<string, string>>();

  /** Currently active language; exposed as a signal so views react to changes. */
  readonly language = signal<AppLanguage>(DEFAULT_LANGUAGE);

  constructor(private readonly http: HttpClient) {}

  /**
   * Loads the German and English dictionaries and activates the persisted or
   * default language. Must complete before the application is rendered.
   */
  async init(): Promise<void> {
    await Promise.all([this.load('de'), this.load('en')]);
    const stored = this.readStoredLanguage();
    this.language.set(stored ?? DEFAULT_LANGUAGE);
    this.applyDocumentLang();
  }

  /** Switches the active language and persists the choice locally. */
  use(lang: AppLanguage): void {
    this.language.set(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Storage may be unavailable (private mode); ignore and keep in-memory state.
    }
    this.applyDocumentLang();
  }

  /**
   * Resolves a translation key for the active language, falling back to German
   * and finally to the raw key. Supports <c>{name}</c> style placeholders.
   */
  translate(key: string, params?: Record<string, string | number>): string {
    const active = this.dictionaries.get(this.language());
    const fallback = this.dictionaries.get(DEFAULT_LANGUAGE);
    const template = active?.[key] ?? fallback?.[key] ?? key;
    return this.interpolate(template, params);
  }

  private interpolate(template: string, params?: Record<string, string | number>): string {
    if (!params) {
      return template;
    }
    return template.replace(/\{(\w+)\}/g, (_match, token: string) =>
      token in params ? String(params[token]) : `{${token}}`);
  }

  private async load(lang: AppLanguage): Promise<void> {
    try {
      const dict = await firstValueFrom(
        this.http.get<Record<string, string>>(`i18n/${lang}.json`));
      this.dictionaries.set(lang, dict ?? {});
    } catch {
      this.dictionaries.set(lang, {});
    }
  }

  private readStoredLanguage(): AppLanguage | null {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return value === 'de' || value === 'en' ? value : null;
    } catch {
      return null;
    }
  }

  private applyDocumentLang(): void {
    document.documentElement.lang = this.language();
  }
}
