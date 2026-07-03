import { Injectable, computed, signal } from '@angular/core';
import { Tournament } from './models';
import { SideThemeService } from './side-theme.service';
import { TournamentHubService } from './tournament-hub.service';

const STORAGE_KEY = 'judo.activeTournament';

/**
 * Holds the currently selected tournament so that the configuration,
 * registration and draw views all operate on the same context. The selection
 * is persisted locally so a page reload keeps the active tournament.
 * Also manages the SignalR hub connection lifecycle.
 */
@Injectable({ providedIn: 'root' })
export class TournamentContextService {
  private readonly active = signal<Tournament | null>(this.restore());

  constructor(private readonly hub: TournamentHubService, private readonly sideTheme: SideThemeService) {
    // Connect to hub for any tournament already restored from localStorage.
    const restored = this.active();
    if (restored) {
      void this.hub.connect(restored.id);
    }
    this.sideTheme.applyTheme(document.documentElement, restored);
  }

  /** The currently selected tournament, or null when none is chosen. */
  readonly tournament = computed(() => this.active());

  /** The id of the active tournament, or null. */
  readonly tournamentId = computed(() => this.active()?.id ?? null);

  /** Selects the active tournament and persists the choice. */
  select(tournament: Tournament): void {
    this.active.set(tournament);
    void this.hub.connect(tournament.id);
    this.sideTheme.applyTheme(document.documentElement, tournament);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(tournament));
    } catch {
      // Ignore storage failures (e.g. private browsing).
    }
  }

  /** Clears the active tournament (e.g. after it was deleted). */
  clear(): void {
    this.active.set(null);
    void this.hub.disconnect();
    this.sideTheme.applyTheme(document.documentElement, null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Ignore storage failures.
    }
  }

  /** Re-applies a refreshed tournament if it matches the active selection. */
  refreshIfActive(tournament: Tournament): void {
    if (this.active()?.id === tournament.id) {
      this.select(tournament);
    }
  }

  private restore(): Tournament | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return null;
      }

      const parsed: unknown = JSON.parse(raw);
      const tournament = this.normalizeTournament(parsed);
      if (!tournament) {
        localStorage.removeItem(STORAGE_KEY);
        return null;
      }

      return tournament;
    } catch {
      return null;
    }
  }

  private normalizeTournament(value: unknown): Tournament | null {
    if (!value || typeof value !== 'object') {
      return null;
    }

    const x = value as Record<string, unknown>;
    if (!(
      typeof x['id'] === 'string' &&
      typeof x['name'] === 'string' &&
      typeof x['date'] === 'string' &&
      typeof x['venue'] === 'string' &&
      typeof x['organizer'] === 'string' &&
      typeof x['createdAtUtc'] === 'string' &&
      typeof x['updatedAtUtc'] === 'string'
    )) {
      return null;
    }

    return {
      id: x['id'],
      name: x['name'],
      date: x['date'],
      venue: x['venue'],
      organizer: x['organizer'],
      accentSideColor: x['accentSideColor'] === 'Red' ? 'Red' : 'Blue',
      createdAtUtc: x['createdAtUtc'],
      updatedAtUtc: x['updatedAtUtc'],
    };
  }
}
