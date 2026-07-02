import { Injectable, computed, signal } from '@angular/core';
import { Tournament } from './models';
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

  constructor(private readonly hub: TournamentHubService) {
    // Connect to hub for any tournament already restored from localStorage.
    const restored = this.active();
    if (restored) {
      void this.hub.connect(restored.id);
    }
  }

  /** The currently selected tournament, or null when none is chosen. */
  readonly tournament = computed(() => this.active());

  /** The id of the active tournament, or null. */
  readonly tournamentId = computed(() => this.active()?.id ?? null);

  /** Selects the active tournament and persists the choice. */
  select(tournament: Tournament): void {
    this.active.set(tournament);
    void this.hub.connect(tournament.id);
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
      if (!this.isTournament(parsed)) {
        localStorage.removeItem(STORAGE_KEY);
        return null;
      }

      return parsed;
    } catch {
      return null;
    }
  }

  private isTournament(value: unknown): value is Tournament {
    if (!value || typeof value !== 'object') {
      return false;
    }

    const x = value as Record<string, unknown>;
    return (
      typeof x['id'] === 'string' &&
      typeof x['name'] === 'string' &&
      typeof x['date'] === 'string' &&
      typeof x['venue'] === 'string' &&
      typeof x['organizer'] === 'string' &&
      typeof x['createdAtUtc'] === 'string' &&
      typeof x['updatedAtUtc'] === 'string'
    );
  }
}
