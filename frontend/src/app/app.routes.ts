import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { ShellComponent } from './core/layout/shell/shell.component';
import { Permissions } from './core/models/permissions.model';
import { SessionService } from './core/services/session.service';

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
        path: '',
        pathMatch: 'full',
        redirectTo: () =>
          inject(SessionService).hasPermission(Permissions.punchTime) ? 'clock' : 'home',
      },
      {
        path: 'clock',
        canActivate: [permissionGuard(Permissions.punchTime)],
        loadChildren: () => import('./features/clock/clock.routes').then((m) => m.clockRoutes),
      },
      {
        path: 'home',
        title: $localize`:@@homeTitle:ClockIn`,
        loadComponent: () =>
          import('./features/home/pages/home/home.component').then((m) => m.HomeComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
