import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/guards';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
    title: 'Sign in · OpenTicket',
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register').then((m) => m.Register),
    title: 'Create account · OpenTicket',
  },
  {
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/forgot-password').then((m) => m.ForgotPassword),
    title: 'Reset password · OpenTicket',
  },
  {
    // No guest guard: a signed-in user may follow a reset link too, e.g. from another device's email.
    path: 'reset-password',
    loadComponent: () => import('./features/auth/reset-password').then((m) => m.ResetPassword),
    title: 'Choose a new password · OpenTicket',
  },
  {
    path: 'auth/callback',
    loadComponent: () => import('./features/auth/auth-callback').then((m) => m.AuthCallback),
    title: 'Signing in · OpenTicket',
  },
  {
    path: '',
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
        title: 'Dashboard · OpenTicket',
      },
      {
        path: 'board',
        loadComponent: () => import('./features/tickets/ticket-board').then((m) => m.TicketBoard),
        title: 'Board · OpenTicket',
      },
      {
        path: 'backlog',
        canActivate: [roleGuard('Admin', 'Agent')],
        loadComponent: () => import('./features/sprints/backlog-page').then((m) => m.BacklogPage),
        title: 'Backlog · OpenTicket',
      },
      {
        path: 'help',
        loadComponent: () => import('./features/help/guide').then((m) => m.Guide),
        title: 'Guide · OpenTicket',
      },
      { path: 'tickets', loadChildren: () => import('./features/tickets/tickets.routes').then((m) => m.TICKET_ROUTES) },
      {
        path: 'admin',
        canActivate: [roleGuard('Admin')],
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
    ],
  },
  { path: '**', loadComponent: () => import('./features/not-found').then((m) => m.NotFound) },
];
