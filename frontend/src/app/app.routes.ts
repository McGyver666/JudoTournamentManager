import { Routes } from '@angular/router';
import { requireAdminGuard, requireAuthGuard, requireDisplayGuard, requireOperatorGuard } from './core/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tournaments' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'tournaments',
    canActivate: [requireAuthGuard],
    loadComponent: () =>
      import('./features/tournaments/tournaments.component').then((m) => m.TournamentsComponent),
  },
  {
    path: 'config',
    canActivate: [requireOperatorGuard],
    loadComponent: () =>
      import('./features/config/config.component').then((m) => m.ConfigComponent),
  },
  {
    path: 'registrations',
    canActivate: [requireOperatorGuard],
    loadComponent: () =>
      import('./features/registrations/registrations.component').then(
        (m) => m.RegistrationsComponent),
  },
  {
    path: 'category-assignment',
    canActivate: [requireOperatorGuard],
    loadComponent: () =>
      import('./features/category-assignment/category-assignment.component').then(
        (m) => m.CategoryAssignmentComponent),
  },
  {
    path: 'draw',
    canActivate: [requireOperatorGuard],
    loadComponent: () => import('./features/draw/draw.component').then((m) => m.DrawComponent),
  },
  {
    path: 'tatami-assignment',
    canActivate: [requireOperatorGuard],
    loadComponent: () =>
      import('./features/tatami-assignment/tatami-assignment.component').then(
        (m) => m.TatamiAssignmentComponent),
  },
  {
    path: 'match',
    canActivate: [requireOperatorGuard],
    loadComponent: () => import('./features/match/match.component').then((m) => m.MatchComponent),
  },
  {
    path: 'results',
    canActivate: [requireAuthGuard],
    loadComponent: () => import('./features/results/results.component').then((m) => m.ResultsComponent),
  },
  {
    path: 'display',
    canActivate: [requireDisplayGuard],
    loadComponent: () => import('./features/display/display.component').then((m) => m.DisplayComponent),
  },
  {
    path: 'display/tatami/:tatamiId',
    canActivate: [requireDisplayGuard],
    loadComponent: () => import('./features/display/display.component').then((m) => m.DisplayComponent),
  },
  {
    path: 'users',
    canActivate: [requireAdminGuard],
    loadComponent: () => import('./features/user-management/user-management.component').then((m) => m.UserManagementComponent),
  },
  { path: '**', redirectTo: 'tournaments' },
];
