import { WeekStatus } from '../../../shared/models/week-status.model';

/** Mirrors the backend's ReviewQueueDto. */
export interface ReviewQueue {
  timesheets: {
    timesheetId: string;
    ownerId: string;
    name: string;
    teamName: string | null;
    weekStart: string;
    totalSeconds: number;
    submittedAt: string | null;
  }[];
  corrections: {
    requestId: string;
    timesheetId: string;
    ownerId: string;
    name: string;
    teamName: string | null;
    weekStart: string;
    timesheetStatus: WeekStatus;
    reason: string;
    requestedAt: string;
  }[];
}
