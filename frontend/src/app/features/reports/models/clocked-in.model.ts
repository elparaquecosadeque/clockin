/** Mirrors the backend's ClockedInDto. `overLimit` flags an entry open for more than 16 hours. */
export interface ClockedIn {
  userId: string;
  name: string;
  teamName: string | null;
  since: string;
  seconds: number;
  overLimit: boolean;
}
