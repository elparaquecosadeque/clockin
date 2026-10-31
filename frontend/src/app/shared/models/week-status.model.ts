/** A week's timesheet status, or NotStarted for a week with no timesheet yet. Mirrors the backend's WeekStatus. */
export type WeekStatus = 'NotStarted' | 'Draft' | 'Submitted' | 'Approved';
