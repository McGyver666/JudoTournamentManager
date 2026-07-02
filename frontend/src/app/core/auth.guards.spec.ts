import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthStateService } from './auth-state.service';
import { requireAdminGuard, requireAuthGuard, requireDisplayGuard, requireOperatorGuard } from './auth.guards';

describe('auth guards', () => {
  function configure(authState: { isAuthenticated: () => boolean; isAdmin: () => boolean; canOperate: () => boolean; canDisplay: () => boolean }) {
    const loginTree = { path: '/login' };
    const tournamentsTree = { path: '/tournaments' };

    const parseUrl = jasmine.createSpy('parseUrl').and.callFake((url: string) => {
      if (url === '/login') {
        return loginTree as never;
      }

      return tournamentsTree as never;
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStateService, useValue: authState },
        { provide: Router, useValue: { parseUrl } },
      ],
    });

    return { parseUrl, loginTree, tournamentsTree };
  }

  it('requireAuthGuard redirects to /login when unauthenticated', () => {
    const { loginTree } = configure({
      isAuthenticated: () => false,
      isAdmin: () => false,
      canOperate: () => false,
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
      canDisplay: () => true,
    });

    const result = TestBed.runInInjectionContext(() => requireDisplayGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });
});
