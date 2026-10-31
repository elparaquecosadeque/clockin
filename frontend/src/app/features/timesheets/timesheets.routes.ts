import { Routes } from '@angular/router';
import { TimesheetComponent } from './pages/timesheet/timesheet.component';

const title = $localize`:@@timesheetTitle:Timesheet · ClockIn`;

/** The signed-in user's own weeks, under /timesheets. */
export const ownTimesheetRoutes: Routes = [
  { path: '', component: TimesheetComponent, title },
  { path: ':weekStart', component: TimesheetComponent, title },
];

/** The review queue and the weeks it links to, under /review. */
export const reviewRoutes: Routes = [
  {
    path: '',
    title: $localize`:@@reviewPageTitle:Review · ClockIn`,
    loadComponent: () =>
      import('./pages/review-queue/review-queue.component').then((m) => m.ReviewQueueComponent),
  },
  { path: ':ownerId/:weekStart', component: TimesheetComponent, title },
];
