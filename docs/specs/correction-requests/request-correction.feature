Feature: Request a correction
  When a week is no longer a Draft, the owner asks the approver to reopen it.

  Background:
    Given Eve is signed in

  Scenario Outline: Requests a correction on a locked timesheet
    Given Eve's timesheet for week 2026-10-19 is <status>
    When Eve requests a correction with reason "Wednesday shift is missing"
    Then a Pending correction request exists for that timesheet
    And it is in Sam's queue
    And the timesheet history records "CorrectionRequested" by Eve with the reason

    Examples:
      | status    |
      | Submitted |
      | Approved  |

  Scenario: Sends a supervisor's request to the admin queue
    Given Sam is signed in
    And Sam's timesheet for week 2026-10-19 is Approved
    When Sam requests a correction with a reason
    Then the request is in the admin queue

  Scenario: Rejects a request on a Draft, which can simply be edited
    Given Eve's timesheet for week 2026-10-19 is a Draft
    When Eve requests a correction with a reason
    Then it breaks the rule "TimesheetIsEditable"

  Scenario Outline: Rejects a missing reason
    Given Eve's timesheet for week 2026-10-19 is Approved
    When Eve requests a correction with reason "<reason>"
    Then it fails validation on "reason"

    Examples:
      | reason     |
      |            |
      | "   "      |
      | 1001 chars |

  Rule: At most one pending request per timesheet

    Scenario: Rejects a second pending request
      Given Eve's timesheet for week 2026-10-19 is Approved
      And Eve has a pending correction request on it
      When Eve requests a correction with a reason
      Then it conflicts with "CorrectionRequestAlreadyPending"

    Scenario: Allows a new request after a decline
      Given Eve's timesheet for week 2026-10-19 is Approved
      And Sam declined Eve's earlier correction request on it
      When Eve requests a correction with a reason
      Then a Pending correction request exists for that timesheet

  Scenario: Forbids requesting on someone else's timesheet
    Given Ed's timesheet for week 2026-10-19 is Approved
    When Eve requests a correction on Ed's timesheet
    Then it is forbidden
