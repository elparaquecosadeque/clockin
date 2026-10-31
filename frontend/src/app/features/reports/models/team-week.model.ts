import { WeekStatus } from '../../../shared/models/week-status.model';

export interface TeamWeekRow {
  userId: string;
  name: string;
  status: WeekStatus;
  timesheetId: string | null;
  /** Monday first. */
  daySeconds: number[];
  openDay: number | null;
  totalSeconds: number;
}

/** A team, or the supervisors (`isSupervisors`, admins only) as their own group. */
export interface TeamWeekGroup {
  teamId: string | null;
  teamName: string | null;
  supervisorName: string | null;
  isSupervisors: boolean;
  summary: { notStarted: number; draft: number; submitted: number; approved: number };
  rows: TeamWeekRow[];
}

/** Mirrors the backend's TeamWeekDto. */
export interface TeamWeek {
  weekStart: string;
  groups: TeamWeekGroup[];
  timeZone: string;
}
