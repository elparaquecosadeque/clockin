import { Routes } from '@angular/router';
import { TeamsComponent } from './pages/teams/teams.component';
import { UsersComponent } from './pages/users/users.component';

export const usersRoutes: Routes = [
  { path: '', component: UsersComponent, title: $localize`:@@usersPageTitle:Users · ClockIn` },
];

export const teamsRoutes: Routes = [
  { path: '', component: TeamsComponent, title: $localize`:@@teamsPageTitle:Teams · ClockIn` },
];
