import { Routes } from '@angular/router';
import { TeamWeekComponent } from './pages/team-week/team-week.component';

const title = $localize`:@@teamWeekPageTitle:Team week · ClockIn`;

export const reportsRoutes: Routes = [
  { path: '', component: TeamWeekComponent, title },
  { path: ':weekStart', component: TeamWeekComponent, title },
];
