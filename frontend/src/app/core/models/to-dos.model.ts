/** Mirrors the backend's ToDosDto. */
export interface ToDos {
  clockedInSince: string | null;
  returnedWeeks: string[];
  unsubmittedWeeks: string[];
  declinedCorrections: number;
  timesheetsToReview: number;
  correctionsToDecide: number;
}
