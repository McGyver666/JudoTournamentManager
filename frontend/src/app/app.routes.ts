import { Routes } from '@angular/router';
import { requireAdminGuard, requireAuthGuard, requireDisplayGuard, requireLiveOperationGuard, requireOperatorGuard, requireTournamentContextGuard } from './core/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tournaments' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'tournament-overview',
    canActivate: [requireTournamentContextGuard],
    loadComponent: () =>
      import('./features/tournament-overview/tournament-overview.component').then(
        (m) => m.TournamentOverviewComponent),
  },
  {
    path: 'tournaments',
    canActivate: [requireAuthGuard],
    loadComponent: () =>
      import('./features/tournaments/tournaments.component').then((m) => m.TournamentsComponent),
  },
  {
    path: 'config',
    canActivate: [requireTournamentContextGuard, requireOperatorGuard],
    loadComponent: () =>
      import('./features/config/config.component').then((m) => m.ConfigComponent),
  },
  {
    path: 'registrations',
    canActivate: [requireTournamentContextGuard, requireOperatorGuard],
    loadComponent: () =>
      import('./features/registrations/registrations.component').then(
        (m) => m.RegistrationsComponent),
  },
  {
    path: 'category-assignment',
    canActivate: [requireTournamentContextGuard, requireOperatorGuard],
    loadComponent: () =>
      import('./features/category-assignment/category-assignment.component').then(
        (m) => m.CategoryAssignmentComponent),
  },
  {
    path: 'draw',
    canActivate: [requireTournamentContextGuard, requireOperatorGuard],
    loadComponent: () => import('./features/draw/draw.component').then((m) => m.DrawComponent),
  },
  {
    path: 'draw/print-match-lists',
    canActivate: [requireTournamentContextGuard, requireOperatorGuard],
    loadComponent: () =>
      import('./features/match-lists/match-lists.component').then((m) => m.MatchListsComponent),
  },
  {
    path: 'tatami-assignment',
    canActivate: [requireTournamentContextGuard, requireOperatorGuard],
    loadComponent: () =>
      import('./features/tatami-assignment/tatami-assignment.component').then(
        (m) => m.TatamiAssignmentComponent),
  },
  {
    path: 'team-matchday',
    canActivate: [requireTournamentContextGuard, requireOperatorGuard],
    loadComponent: () =>
      import('./features/team-matchday/team-matchday.component').then((m) => m.TeamMatchdayComponent),
  },
  {
    path: 'combat-overview',
    canActivate: [requireTournamentContextGuard, requireAuthGuard],
    loadComponent: () =>
      import('./features/combat-overview/combat-overview.component').then(
        (m) => m.CombatOverviewComponent),
  },
  {
    path: 'match',
    canActivate: [requireTournamentContextGuard, requireLiveOperationGuard],
    loadComponent: () => import('./features/match/match.component').then((m) => m.MatchComponent),
  },
  {
    path: 'results',
    canActivate: [requireTournamentContextGuard, requireAuthGuard],
    loadComponent: () => import('./features/results/results.component').then((m) => m.ResultsComponent),
  },
  {
    path: 'display',
    canActivate: [requireTournamentContextGuard, requireDisplayGuard],
    loadComponent: () => import('./features/display/display.component').then((m) => m.DisplayComponent),
  },
  {
    path: 'display/match-lists',
    canActivate: [requireTournamentContextGuard, requireDisplayGuard],
    loadComponent: () =>
      import('./features/match-lists/match-lists.component').then((m) => m.MatchListsComponent),
  },
  {
    path: 'display/tatami/:tatamiId',
    canActivate: [requireTournamentContextGuard, requireDisplayGuard],
    loadComponent: () => import('./features/display/display.component').then((m) => m.DisplayComponent),
  },
  {
    path: 'users',
    canActivate: [requireAdminGuard],
    loadComponent: () => import('./features/user-management/user-management.component').then((m) => m.UserManagementComponent),
  },
  {
    // Anonymous guest access via QR share link (?tid=…&t=<token>).
    // No guard: the bearer token is supplied in the URL and validated server-side.
    path: 'public/match-lists',
    loadComponent: () =>
      import('./features/match-lists/match-lists.component').then((m) => m.MatchListsComponent),
  },
  { path: '**', redirectTo: 'tournaments' },
];
