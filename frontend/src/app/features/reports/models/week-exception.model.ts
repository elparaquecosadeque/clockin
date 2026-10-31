/** Mirrors the backend's ExceptionDto. */
export interface WeekException {
  kind: 'OverdueDraft' | 'LongEntry' | 'ManyManualChanges';
  userId: string;
  name: string;
  teamName: string | null;
  weekStart: string;
  timesheetId: string;
  entryId: string | null;
  seconds: number | null;
  count: number | null;
}
