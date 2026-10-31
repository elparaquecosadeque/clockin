/** Mirrors the backend's DemoAccountsDto (ADR 0005). */
export interface DemoAccounts {
  password: string;
  accounts: DemoAccount[];
}

export type DemoScenario =
  | 'NotStarted'
  | 'ClockedIn'
  | 'ClockedOut'
  | 'ForgotToClockOut'
  | 'LockedOut'
  | 'PendingActivation'
  | 'Deactivated'
  | 'Admin';

export interface DemoAccount {
  email: string;
  name: string;
  role: 'Employee' | 'Supervisor' | 'Admin';
  /** The employee's team, or the team a supervisor leads. */
  team: string | null;
  scenario: DemoScenario;
}
