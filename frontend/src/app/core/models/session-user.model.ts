import { TimeFormat } from '../../shared/models/time-format.model';

/** Mirrors the backend's MeDto. */
export interface SessionUser {
  id: string;
  name: string;
  email: string;
  role: 'Employee' | 'Supervisor' | 'Admin';
  permissions: string[];
  language: 'en' | 'es';
  timeFormat: TimeFormat;
}
