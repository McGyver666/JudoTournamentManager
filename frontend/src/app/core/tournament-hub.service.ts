import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { AuthStateService } from './auth-state.service';
import { Fight, FightUpdatedMessage } from './models';

export interface CategoryFightsUpdatedEvent {
  tournamentId: string;
  categoryId: string;
}

/**
 * Wraps the SignalR connection to the TournamentHub.
 * Components subscribe to fightUpdated$ and categoryFightsUpdated$ for
 * real-time bracket/queue updates without polling.
 *
 * Connection lifecycle is managed by TournamentContextService:
 * call connect() when a tournament is selected, disconnect() when cleared.
 */
@Injectable({ providedIn: 'root' })
export class TournamentHubService {
  private connection: signalR.HubConnection | null = null;
  private currentTournamentId: string | null = null;

  constructor(private readonly auth: AuthStateService) {}

  /** True while the SignalR connection is in the Connected state. */
  readonly connected = signal(false);

  private readonly _fightUpdated = new Subject<Fight>();
  private readonly _categoryFightsUpdated = new Subject<CategoryFightsUpdatedEvent>();
  private readonly _serverTimeSync = new Subject<string>();
  private readonly _reconnected = new Subject<void>();

  /** Emits whenever a single fight's state changes (start / score / tatami assign). */
  readonly fightUpdated$: Observable<Fight> = this._fightUpdated.asObservable();
  readonly serverTimeSync$: Observable<string> = this._serverTimeSync.asObservable();
  readonly reconnected$: Observable<void> = this._reconnected.asObservable();

  /**
   * Emits whenever the full fights list for a category changed
   * (draw generated / result confirmed / result corrected).
   * Components should re-fetch the category's fight list on receipt.
   */
  readonly categoryFightsUpdated$: Observable<CategoryFightsUpdatedEvent> =
    this._categoryFightsUpdated.asObservable();

  /** Connects to the hub and joins the given tournament group. */
  async connect(tournamentId: string): Promise<void> {
    if (this.currentTournamentId === tournamentId && this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    await this.disconnect();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/tournament', {
        accessTokenFactory: () => this.auth.token() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    this.connection.on('FightUpdated', (payload: Fight | FightUpdatedMessage) => {
      if (payload && typeof payload === 'object' && 'fight' in payload) {
        if (payload.serverNowUtc) {
          this._serverTimeSync.next(payload.serverNowUtc);
        }
        this._fightUpdated.next(payload.fight);
        return;
      }

      this._fightUpdated.next(payload as Fight);
    });
    this.connection.on('CategoryFightsUpdated', (evt: CategoryFightsUpdatedEvent) =>
      this._categoryFightsUpdated.next(evt));

    this.connection.onreconnected(() => {
      this.connected.set(true);
      this._reconnected.next();
      void this.connection?.invoke('JoinTournamentAsync', tournamentId);
    });
    this.connection.onclose(() => this.connected.set(false));

    try {
      await this.connection.start();
      await this.connection.invoke('JoinTournamentAsync', tournamentId);
      this.connected.set(true);
      this.currentTournamentId = tournamentId;
    } catch {
      // SignalR is enhancement only; app works offline without it.
      this.connected.set(false);
    }
  }

  /** Leaves the tournament group and stops the connection. */
  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        if (this.currentTournamentId) {
          await this.connection.invoke('LeaveTournamentAsync', this.currentTournamentId);
        }
        await this.connection.stop();
      } catch {
        // Ignore cleanup errors.
      }
      this.connection = null;
      this.currentTournamentId = null;
      this.connected.set(false);
    }
  }
}
