Feature: Move an employee to another team

  Background:
    Given Ada is signed in

  Scenario: Moves the employee and their pending work
    Given Eve has a Submitted timesheet for week 2026-10-19
    And Eve has a pending correction request for week 2026-10-12
    When Ada moves Eve to team "Office"
    Then Eve is in team "Office"
    And Sue's queue holds Eve's Submitted timesheet and correction request
    And Sam can no longer approve, return or decide them

  Scenario: Allows moving an employee who is clocked in
    Given Eve clocked in at 08:00 and has not clocked out
    When Ada moves Eve to team "Office"
    Then Eve is in team "Office"
    And Eve's entry is still open

  Scenario: Rejects the same team
    When Ada moves Eve to team "Warehouse"
    Then it breaks the rule "AlreadyInTeam"

  Scenario: Rejects an archived team
    Given team "Old Store" is archived
    When Ada moves Eve to team "Old Store"
    Then it breaks the rule "TeamIsArchived"

  Scenario Outline: Rejects users who are not movable employees
    When Ada moves <user> to team "Office"
    Then it breaks the rule "<code>"

    Examples:
      | user          | code              |
      | Sam           | UserIsNotEmployee |
      | Abe           | UserIsNotEmployee |
      | deactivated Ed | UserIsDeactivated |

  Scenario: Forbids supervisors
    Given Sam is signed in
    When Sam moves Eve to team "Office"
    Then it is forbidden
