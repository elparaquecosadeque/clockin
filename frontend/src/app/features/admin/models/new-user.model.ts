import { Role } from './user-list-item.model';

/** The body of POST /api/users. */
export interface NewUser {
  email: string;
  name: string;
  role: Role;
  teamId: string | null;
  language: 'en' | 'es';
  timeFormat: '24h' | '12h';
}
