/** Mirrors the backend's TeamListItemDto. `employeeCount` leaves out deactivated members. */
export interface TeamListItem {
  id: string;
  name: string;
  supervisorId: string;
  supervisorName: string;
  isArchived: boolean;
  employeeCount: number;
}
