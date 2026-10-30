/** Mirrors the backend's TimeEntryDto. */
export interface TimeEntry {
  id: string;
  timesheetId: string;
  start: string;
  end: string | null;
  isManuallyChanged: boolean;
}

/** Mirrors the backend's ClockStatusDto. Totals cover closed entries; the open one is added live. */
export interface ClockStatus {
  openEntry: TimeEntry | null;
  openEntryStartedToday: boolean;
  openEntryStartedThisWeek: boolean;
  todayEntries: TimeEntry[];
  closedTodaySeconds: number;
  closedWeekSeconds: number;
  weekStart: string;
  timeZone: string;
}
