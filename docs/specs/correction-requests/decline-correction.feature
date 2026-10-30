Feature: Decline a correction request

  Background:
    Given Eve's timesheet for week 2026-10-19 is Approved
    And Eve has a pending correction request on it
    And Sam is signed in

  Scenario: Declines with a comment and leaves the timesheet as it was
    When Sam declines the request with comment "those hours were already paid"
    Then the request is Declined by Sam
    And Eve sees the comment "those hours were already paid"
    And the timesheet is still Approved
    And the timesheet history records "CorrectionDeclined" by Sam with the comment

  Scenario: Unblocks approval of a Submitted timesheet
    Given Ed's timesheet for week 2026-10-19 is Submitted with a pending correction request
    When Sam declines Ed's request with a comment
    Then Sam can approve Ed's timesheet

  Scenario Outline: Rejects a missing comment
    When Sam declines the request with comment "<comment>"
    Then it fails validation on "comment"

    Examples:
      | comment |
      |         |
      | "   "   |

  Scenario: Rejects a request that is no longer pending
    Given Sam already accepted the request
    When Sam declines the request with a comment
    Then it breaks the rule "CorrectionRequestNotPending"

  Scenario: Forbids a supervisor of another team
    Given Sue is signed in
    When Sue declines the request with a comment
    Then it is forbidden
