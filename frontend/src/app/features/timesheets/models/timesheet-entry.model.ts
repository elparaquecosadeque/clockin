/** Mirrors the backend's TimesheetEntryDto. Punched values are set once a punched entry was edited. */
export interface TimesheetEntry {
  id: string;
  date: string;
  start: string;
  end: string | null;
  seconds: number;
  manualChange: 'None' | 'Added' | 'Edited';
  changedByName: string | null;
  note: string | null;
  punchedStart: string | null;
  punchedEnd: string | null;
}
