Feature: Change a team's supervisor
  The employee's supervisor is derived from the team, so pending work follows the team.

  Background:
    Given Ada is signed in
    And "Sid" is an active Supervisor who leads no team

  Scenario: Hands the team and its pending work to the new supervisor
    Given Eve has a Submitted timesheet for week 2026-10-19
    And Ed has a pending correction request
    And Sam approved Eve's timesheet for week 2026-10-12
    When Ada makes Sid the supervisor of team "Warehouse"
    Then Sid's queue holds Eve's Submitted timesheet and Ed's correction request
    And Sam's queue is empty
    And Eve's timesheet for week 2026-10-12 still shows it was approved by Sam
    And Sam leads no team and can be given another one

  Scenario: Rejects a supervisor who already leads a team
    When Ada makes Sue the supervisor of team "Warehouse"
    Then it breaks the rule "SupervisorAlreadyLeadsTeam"

  Scenario: Rejects the current supervisor
    When Ada makes Sam the supervisor of team "Warehouse"
    Then it breaks the rule "SupervisorAlreadyLeadsTeam"

  Scenario Outline: Rejects a user who cannot lead
    When Ada makes <user> the supervisor of team "Warehouse"
    Then it breaks the rule "<code>"

    Examples:
      | user                | code                |
      | Eve                 | UserIsNotSupervisor |
      | a deactivated "Rob" | UserIsDeactivated   |

  Scenario: Rejects an archived team
    Given team "Old Store" is archived
    When Ada makes Sid the supervisor of team "Old Store"
    Then it breaks the rule "TeamIsArchived"

  Scenario: Forbids non-admins
    Given Sam is signed in
    When Sam makes Sid the supervisor of team "Warehouse"
    Then it is forbidden
