/** Mirrors the backend's MeDto. */
export interface SessionUser {
  id: string;
  name: string;
  email: string;
  role: 'Employee' | 'Supervisor' | 'Admin';
  permissions: string[];
}
