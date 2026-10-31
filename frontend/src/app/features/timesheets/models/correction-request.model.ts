/** Mirrors the backend's CorrectionRequestDto. */
export interface CorrectionRequest {
  id: string;
  status: 'Pending' | 'Accepted' | 'Declined';
  reason: string;
  requestedAt: string;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionComment: string | null;
}
