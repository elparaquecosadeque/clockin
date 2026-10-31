import { CorrectionRequest } from './correction-request.model';
import { HistoryItem } from './history-item.model';
import { TimesheetEntry } from './timesheet-entry.model';
import { WeekStatus } from '../../../shared/models/week-status.model';

/** Mirrors the backend's TimesheetDto. `id` is null and `version` 0 for a week with no timesheet yet. */
export interface Timesheet {
  id: string | null;
  version: number;
  ownerId: string;
  ownerName: string;
  teamName: string | null;
  weekStart: string;
  status: WeekStatus;
  returnComment: string | null;
  approval: { approverName: string; at: string } | null;
  totalSeconds: number;
  days: { date: string; seconds: number; hasOpenEntry: boolean }[];
  entries: TimesheetEntry[];
  history: HistoryItem[];
  latestCorrection: CorrectionRequest | null;
  can: {
    changeEntries: boolean;
    submit: boolean;
    approve: boolean;
    return: boolean;
    reopen: boolean;
    requestCorrection: boolean;
    decideCorrection: boolean;
  };
  timeZone: string;
}
