import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthStateService } from './auth-state.service';
import { TournamentContextService } from './tournament-context.service';
import { requireAdminGuard, requireAuthGuard, requireDisplayGuard, requireLiveOperationGuard, requireOperatorGuard, requireTournamentContextGuard } from './auth.guards';

describe('auth guards', () => {
  function configure(authState: { isAuthenticated: () => boolean; isAdmin: () => boolean; canOperate: () => boolean; canOperateLive: () => boolean; canDisplay: () => boolean }) {
    const loginTree = { path: '/login' };
    const tournamentsTree = { path: '/tournaments' };
    const tournamentId = signal<string | null>(null);

    const parseUrl = jasmine.createSpy('parseUrl').and.callFake((url: string) => {
      if (url === '/login') {
        return loginTree as never;
      }

      return tournamentsTree as never;
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStateService, useValue: authState },
        { provide: TournamentContextService, useValue: { tournamentId } },
        { provide: Router, useValue: { parseUrl } },
      ],
    });

    return { parseUrl, loginTree, tournamentsTree, tournamentId };
  }

  it('requireAuthGuard redirects to /login when unauthenticated', () => {
    const { loginTree } = configure({
      isAuthenticated: () => false,
      isAdmin: () => false,
      canOperate: () => false,
      canOperateLive: () => false,
      canDisplay: () => false,
    });

    const result = TestBed.runInInjectionContext(() => requireAuthGuard({} as never, {} as never));

    expect(result).toBe(loginTree as never);
  });

  it('requireAdminGuard blocks display role and redirects to /tournaments', () => {
    const { tournamentsTree } = configure({
      isAuthenticated: () => true,
      isAdmin: () => false,
      canOperate: () => false,
      canOperateLive: () => false,
      canDisplay: () => false,
    });

    const result = TestBed.runInInjectionContext(() => requireAdminGuard({} as never, {} as never));

    expect(result).toBe(tournamentsTree as never);
  });

  it('requireOperatorGuard allows operator role', () => {
    configure({
      isAuthenticated: () => true,
      isAdmin: () => false,
      canOperate: () => true,
      canOperateLive: () => true,
      canDisplay: () => true,
    });

    const result = TestBed.runInInjectionContext(() => requireOperatorGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('requireDisplayGuard allows display role', () => {
    configure({
      isAuthenticated: () => true,
      isAdmin: () => false,
      canOperate: () => false,
      canOperateLive: () => true,
      canDisplay: () => true,
    });

    const result = TestBed.runInInjectionContext(() => requireDisplayGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('requireLiveOperationGuard allows competition role', () => {
    configure({
      isAuthenticated: () => true,
      isAdmin: () => false,
      canOperate: () => false,
      canOperateLive: () => true,
      canDisplay: () => true,
    });

    const result = TestBed.runInInjectionContext(() => requireLiveOperationGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('requireTournamentContextGuard redirects an authenticated user without a tournament', () => {
    const { tournamentsTree } = configure({
      isAuthenticated: () => true,
      isAdmin: () => false,
      canOperate: () => false,
      canOperateLive: () => true,
      canDisplay: () => true,
    });
    const result = TestBed.runInInjectionContext(() => requireTournamentContextGuard({} as never, {} as never));

    expect(result).toBe(tournamentsTree as never);
  });

  it('requireTournamentContextGuard allows an authenticated user with a selected tournament', () => {
    const { tournamentId } = configure({
      isAuthenticated: () => true,
      isAdmin: () => false,
      canOperate: () => false,
      canOperateLive: () => true,
      canDisplay: () => true,
    });
    tournamentId.set('tournament-1');

    const result = TestBed.runInInjectionContext(() => requireTournamentContextGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });
});
