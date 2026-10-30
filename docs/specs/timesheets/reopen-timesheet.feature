Feature: Reopen an approved timesheet
  Approved -> Draft. Admins do it directly with a reason; supervisors only by
  accepting a correction request.

  Background:
    Given Eve's timesheet for week 2026-10-19 was Approved by Sam
    And Ada is signed in

  Scenario: An admin reopens an approved timesheet
    When Ada reopens it with reason "payroll found a missing shift"
    Then it is a Draft
    And it no longer shows a current approver
    And the history keeps "Approved" by Sam, followed by "Reopened" by Ada with the reason

  Scenario: Reopens a deactivated employee's timesheet for an admin fix
    Given Ed is deactivated and Ed's timesheet for week 2026-10-19 is Approved
    When Ada reopens it with a reason
    Then Ada can edit its entries with a reason and submit it on Ed's behalf

  Scenario: Rejects a missing reason
    When Ada reopens it without a reason
    Then it fails validation on "reason"

  Scenario Outline: Rejects a timesheet that is not Approved
    Given Eve's timesheet for week 2026-10-19 is <status> instead
    When Ada reopens it with a reason
    Then it breaks the rule "TimesheetNotApproved"

    Examples:
      | status    |
      | Draft     |
      | Submitted |

  Scenario: Rejects a timesheet with a pending correction request
    Given Eve has a pending correction request on it
    When Ada reopens it with a reason
    Then it breaks the rule "PendingCorrectionRequest"

  Scenario Outline: Forbids non-admins
    Given <user> is signed in
    When <user> reopens it with a reason
    Then it is forbidden

    Examples:
      | user |
      | Sam  |
      | Eve  |
