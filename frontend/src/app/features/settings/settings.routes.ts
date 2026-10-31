import { Routes } from '@angular/router';
import { SettingsComponent } from './pages/settings/settings.component';

export const settingsRoutes: Routes = [
  {
    path: '',
    component: SettingsComponent,
    title: $localize`:@@settingsPageTitle:Settings · ClockIn`,
  },
];
