Feature: Deactivate a user
  Users are never deleted. Deactivation keeps every record and blocks access at once.

  Background:
    Given Ada is signed in

  Scenario: Deactivates an employee
    Given Eve has a Draft timesheet for week 2026-10-19 and a Submitted one for week 2026-10-12
    When Ada deactivates Eve
    Then Eve has status Deactivated
    And Eve's next request is unauthorized because the security stamp changed
    And every refresh token and access link of Eve is revoked
    And the Draft timesheet is unchanged
    And the Submitted timesheet is still in Sam's queue
    And the Audit log records the deactivation

  Scenario: Deactivates a pending user
    Given "Fay" is PendingActivation with a valid link
    When Ada deactivates Fay
    Then Fay has status Deactivated
    And Fay's link no longer works

  Scenario: Deactivates another admin
    When Ada deactivates Abe
    Then Abe has status Deactivated

  Rule: Nothing is left dangling

    Scenario: Rejects a user with an open entry
      Given Eve clocked in at 08:00 and has not clocked out
      When Ada deactivates Eve
      Then it breaks the rule "OpenEntryMustBeClosed"

    Scenario: Rejects a supervisor who leads a team
      When Ada deactivates Sam
      Then it breaks the rule "TeamNeedsNewSupervisor"

    Scenario: Deactivates a supervisor who no longer leads a team
      Given team "Warehouse" is now led by "Sid"
      When Ada deactivates Sam
      Then Sam has status Deactivated

  Rule: There is always an active admin

    Scenario: Rejects deactivating oneself
      When Ada deactivates Ada
      Then it breaks the rule "CannotDeactivateSelf"

  Scenario: Rejects a user who is already deactivated
    Given Ed is deactivated
    When Ada deactivates Ed
    Then it breaks the rule "UserIsDeactivated"

  Scenario Outline: Forbids non-admins
    Given <user> is signed in
    When <user> deactivates Ed
    Then it is forbidden

    Examples:
      | user |
      | Sam  |
      | Eve  |
