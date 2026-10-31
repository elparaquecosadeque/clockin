export type TimesheetEventKind =
  | 'Submitted'
  | 'Returned'
  | 'Approved'
  | 'Reopened'
  | 'CorrectionRequested'
  | 'CorrectionDeclined'
  | 'EntryAdded'
  | 'EntryEdited'
  | 'EntryDeleted';

export interface EntryTimes {
  entryId: string;
  start: string;
  end: string | null;
  punchedStart: string | null;
  punchedEnd: string | null;
}

/** Mirrors the backend's HistoryItemDto. `byOwner` false on Submitted means "on behalf of". */
export interface HistoryItem {
  kind: TimesheetEventKind;
  actorName: string;
  byOwner: boolean;
  at: string;
  comment: string | null;
  before: EntryTimes | null;
  after: EntryTimes | null;
}
