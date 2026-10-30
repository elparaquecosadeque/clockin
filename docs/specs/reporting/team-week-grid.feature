Feature: Team week grid
  One row per person, one column per day, plus the week total and timesheet status.
  Hours from Draft timesheets are included, so the grid is live.

  Background:
    Given it is Friday 2026-10-30 10:00

  Scenario: Shows the supervisor's team for a week
    Given Sam is signed in
    And Eve has 8 hours on Monday and 7.5 hours on Tuesday of week 2026-10-26
    When Sam views the grid for week 2026-10-26
    Then there is one row for Eve and one for Ed
    And Eve's row shows 8:00 on Monday, 7:30 on Tuesday and a total of 15:30
    And each row shows the timesheet status

  Scenario: Shows "Not started" for someone with no timesheet that week
    Given Sam is signed in
    And Ed has no timesheet for week 2026-10-26
    When Sam views the grid for week 2026-10-26
    Then Ed's row shows status "Not started" and a total of 0:00

  Scenario: Includes a deactivated employee only in weeks with a timesheet
    Given Sam is signed in
    And "Rob" in team "Warehouse" was deactivated with a timesheet for week 2026-10-12
    When Sam views the grids for weeks 2026-10-12 and 2026-10-26
    Then Rob appears in week 2026-10-12 only

  Scenario: Shows a moved employee under the new team
    Given Ada moved Eve to team "Office" on 2026-10-28
    When Sue views the grid for week 2026-10-26
    Then Eve appears in Sue's grid with the whole week
    And Eve does not appear in Sam's grid

  Scenario: Shows the running total of an open entry
    Given Sam is signed in
    And Eve clocked in at 08:00 today and has not clocked out
    When Sam views the grid for week 2026-10-26
    Then Eve's Friday shows 2:00 and is marked as open

  Rule: Admins see every team, optionally filtered

    Scenario: Shows all teams grouped, with a summary per team
      Given Ada is signed in
      When Ada views the grid for week 2026-10-26
      Then rows are grouped by team "Office" and "Warehouse"
      And each team shows its supervisor and how many timesheets are Not started, Draft, Submitted and Approved

    Scenario: Filters by team
      Given Ada is signed in
      When Ada views the grid for week 2026-10-26 filtered to team "Office"
      Then only Olga appears

    Scenario: Shows supervisors as their own group
      Given Ada is signed in
      When Ada views the grid for week 2026-10-26
      Then Sam and Sue appear in a "Supervisors" group

  Scenario Outline: Controls who can see which team
    Given <user> is signed in
    When <user> views the grid for team "<team>"
    Then the result is <result>

    Examples:
      | user | team      | result               |
      | Sam  | Warehouse | the grid is returned |
      | Sam  | Office    | it is forbidden      |
      | Eve  | Warehouse | it is forbidden      |
