Feature: Return a timesheet
  Submitted -> Draft, with a comment telling the owner what to fix.
  The approver never edits entries.

  Background:
    Given Eve's timesheet for week 2026-10-19 is Submitted
    And Sam is signed in

  Scenario: Returns the timesheet with a comment
    When Sam returns it with comment "Tuesday is missing"
    Then it is a Draft
    And Eve sees the return comment "Tuesday is missing"
    And the history records "Returned" by Sam with the comment

  Scenario Outline: Rejects a missing comment
    When Sam returns it with comment "<comment>"
    Then it fails validation on "comment"

    Examples:
      | comment    |
      |            |
      | "   "      |
      | 1001 chars |

  Scenario: Rejects a timesheet with a pending correction request
    Given Eve has a pending correction request on it
    When Sam returns it with a comment
    Then it breaks the rule "PendingCorrectionRequest"

  Scenario Outline: Rejects a timesheet that is not Submitted
    Given Eve's timesheet for week 2026-10-19 is <status>
    When Sam returns it with a comment
    Then it breaks the rule "TimesheetNotSubmitted"

    Examples:
      | status   |
      | Draft    |
      | Approved |

  Scenario: Forbids a supervisor of another team
    Given Sue is signed in
    When Sue returns it with a comment
    Then it is forbidden
