import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthStateService } from './auth-state.service';
import { TournamentContextService } from './tournament-context.service';

export const requireAuthGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.parseUrl('/login');
};

export const requireTournamentContextGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  const context = inject(TournamentContextService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.parseUrl('/login');
  }
  return context.tournamentId() ? true : router.parseUrl('/tournaments');
};

export const requireAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.parseUrl('/login');
  }
  return auth.isAdmin() ? true : router.parseUrl('/tournaments');
};

export const requireOperatorGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.parseUrl('/login');
  }
  return auth.canOperate() ? true : router.parseUrl('/tournaments');
};

export const requireLiveOperationGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.parseUrl('/login');
  }
  return auth.canOperateLive() ? true : router.parseUrl('/tournaments');
};

export const requireDisplayGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.parseUrl('/login');
  }
  return auth.canDisplay() ? true : router.parseUrl('/tournaments');
};
