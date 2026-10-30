Feature: Change a user's role
  Employee and Supervisor can be swapped. Admin is never granted or removed
  by a role change: create a new admin account or deactivate the old one instead.

  Background:
    Given Ada is signed in

  Scenario: Promotes an employee to supervisor
    Given Eve has a Submitted timesheet for week 2026-10-19
    When Ada changes Eve's role to Supervisor
    Then Eve has role Supervisor and no team
    And Eve's Submitted timesheet is in the admin queue
    And Eve's security stamp has changed
    And the Audit log records the permission change

  Scenario: Demotes a supervisor without a team to employee
    Given "Sid" is a Supervisor who leads no team
    When Ada changes Sid's role to Employee in team "Warehouse"
    Then Sid has role Employee in team "Warehouse"

  Scenario: Rejects demoting a supervisor who leads a team
    When Ada changes Sam's role to Employee in team "Office"
    Then it breaks the rule "TeamNeedsNewSupervisor"

  Scenario: Rejects demoting to employee without a team
    Given "Sid" is a Supervisor who leads no team
    When Ada changes Sid's role to Employee without a team
    Then it fails validation on "teamId"

  Scenario Outline: Rejects any change to or from Admin
    When Ada changes <user>'s role to <role>
    Then it breaks the rule "AdminRoleIsFixed"

    Examples:
      | user | role       |
      | Eve  | Admin      |
      | Abe  | Supervisor |
      | Abe  | Employee   |
      | Ada  | Supervisor |

  Scenario: Rejects a deactivated user
    Given Ed is deactivated
    When Ada changes Ed's role to Supervisor
    Then it breaks the rule "UserIsDeactivated"

  Scenario Outline: Forbids non-admins
    Given <user> is signed in
    When <user> changes Ed's role to Supervisor
    Then it is forbidden

    Examples:
      | user |
      | Sam  |
      | Eve  |
