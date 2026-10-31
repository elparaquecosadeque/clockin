export type Role = 'Employee' | 'Supervisor' | 'Admin';

/** Mirrors the backend's UserListItemDto. `ledTeamId` is the active team a supervisor leads. */
export interface UserListItem {
  id: string;
  name: string;
  email: string;
  role: Role;
  status: 'PendingActivation' | 'Active' | 'Deactivated';
  teamId: string | null;
  teamName: string | null;
  ledTeamId: string | null;
  isLockedOut: boolean;
}
