/** Permission names, as the backend's Domain/Accounts/Permissions. */
export const Permissions = {
  punchTime: 'time-entries.punch',
  manageUsers: 'users.manage',
  manageTeams: 'teams.manage',
  reviewTeam: 'timesheets.review-team',
  reviewAll: 'timesheets.review-all',
  manageTimesheets: 'timesheets.manage',
} as const;
