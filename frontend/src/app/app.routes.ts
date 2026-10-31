import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { ShellComponent } from './core/layout/shell/shell.component';
import { Permissions } from './core/models/permissions.model';
import { SessionService } from './core/services/session.service';

const review = [Permissions.reviewTeam, Permissions.reviewAll];

export const routes: Routes = [
  {
    path: 'sign-in',
    title: $localize`:@@signInTitle:Sign in · ClockIn`,
    loadComponent: () =>
      import('./features/auth/pages/sign-in/sign-in.component').then((m) => m.SignInComponent),
  },
  {
    path: 'set-password',
    title: $localize`:@@setPasswordTitle:Set your password · ClockIn`,
    loadComponent: () =>
      import('./features/auth/pages/set-password/set-password.component').then(
        (m) => m.SetPasswordComponent,
      ),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        // Whoever punches time starts at the clock; admins, who don't, at the review queue.
        path: '',
        pathMatch: 'full',
        redirectTo: () => {
          const session = inject(SessionService);
          if (session.hasPermission(Permissions.punchTime)) return 'clock';
          return review.some((p) => session.hasPermission(p)) ? 'review' : 'settings';
        },
      },
      {
        path: 'clock',
        canActivate: [permissionGuard(Permissions.punchTime)],
        loadChildren: () => import('./features/clock/clock.routes').then((m) => m.clockRoutes),
      },
      {
        path: 'timesheets',
        canActivate: [permissionGuard(Permissions.punchTime)],
        loadChildren: () =>
          import('./features/timesheets/timesheets.routes').then((m) => m.ownTimesheetRoutes),
      },
      {
        path: 'review',
        canActivate: [permissionGuard(...review)],
        loadChildren: () =>
          import('./features/timesheets/timesheets.routes').then((m) => m.reviewRoutes),
      },
      {
        path: 'team',
        canActivate: [permissionGuard(...review)],
        loadChildren: () =>
          import('./features/reports/reports.routes').then((m) => m.reportsRoutes),
      },
      {
        path: 'users',
        canActivate: [permissionGuard(Permissions.manageUsers)],
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.usersRoutes),
      },
      {
        path: 'teams',
        canActivate: [permissionGuard(Permissions.manageTeams)],
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.teamsRoutes),
      },
      {
        path: 'settings',
        loadChildren: () =>
          import('./features/settings/settings.routes').then((m) => m.settingsRoutes),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
