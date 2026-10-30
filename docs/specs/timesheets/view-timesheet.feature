Feature: View a timesheet and its history
  The owner, the current approver and admins can see a timesheet in full.

  Background:
    Given Eve's timesheet for week 2026-10-19 has entries, one manual edit, one deleted entry,
      a return and an approval

  Scenario: Shows the timesheet with totals and flags
    Given Eve is signed in
    When Eve views the timesheet for week 2026-10-19
    Then Eve sees status, total hours and hours per day
    And each entry shows its times, note, and whether it was manually added or edited, and by whom
    And edited entries show their punched values

  Scenario: Shows the full history in order
    Given Eve is signed in
    When Eve views the history of the timesheet for week 2026-10-19
    Then every transition is listed oldest first with actor, time and comment or reason
    And every entry change lists before and after values, editor and note
    And the deleted entry appears with its last values

  Scenario: Counts hours on the day an entry started
    Given Eve has an entry from Tuesday 2026-10-20 22:00 to Wednesday 2026-10-21 02:00
    When Eve views the timesheet for week 2026-10-19
    Then Tuesday shows 4 hours and Wednesday shows none from that entry

  Scenario: Includes an open entry up to now in the running total
    Given it is Friday 2026-10-30 10:00
    And Eve clocked in at 08:00 and has not clocked out
    When Eve views the timesheet for week 2026-10-26
    Then today's total includes 2 hours from the open entry
    And the entry is marked as open

  Scenario Outline: Controls who can see it
    Given <user> is signed in
    When <user> views Eve's timesheet for week 2026-10-19
    Then the result is <result>

    Examples:
      | user | result                    |
      | Eve  | the timesheet is returned |
      | Sam  | the timesheet is returned |
      | Ada  | the timesheet is returned |
      | Ed   | it is forbidden           |
      | Sue  | it is forbidden           |

  Scenario: Hides the timesheet from a former supervisor
    Given Ada moved Eve to team "Office"
    And Sam is signed in
    When Sam views Eve's timesheet for week 2026-10-19
    Then it is forbidden
