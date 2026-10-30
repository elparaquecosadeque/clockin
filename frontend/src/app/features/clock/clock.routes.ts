import { Routes } from '@angular/router';
import { ClockComponent } from './pages/clock/clock.component';

export const clockRoutes: Routes = [
  { path: '', component: ClockComponent, title: $localize`:@@clockTitle:Clock · ClockIn` },
];
