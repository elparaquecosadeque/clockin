Feature: Accept a correction request
  Accepting reopens the timesheet to Draft in the same transaction,
  through the CorrectionRequestAccepted event.

  Scenario Outline: Reopens the timesheet
    Given Eve's timesheet for week 2026-10-19 is <status>
    And Eve has a pending correction request on it
    And Sam is signed in
    When Sam accepts the request
    Then the request is Accepted by Sam
    And the timesheet is a Draft
    And the timesheet history records "Reopened" by Sam via that correction request

    Examples:
      | status    |
      | Submitted |
      | Approved  |

  Scenario: Accepts with an optional comment
    Given Eve has a pending correction request on an Approved timesheet
    And Sam is signed in
    When Sam accepts the request with comment "go ahead"
    Then Eve sees the comment "go ahead"

  Scenario: An admin accepts a supervisor's request
    Given Sam has a pending correction request on an Approved timesheet
    And Ada is signed in
    When Ada accepts the request
    Then Sam's timesheet is a Draft

  Scenario Outline: Rejects a request that is no longer pending
    Given Eve's correction request is <status>
    And Sam is signed in
    When Sam accepts the request
    Then it breaks the rule "CorrectionRequestNotPending"

    Examples:
      | status   |
      | Accepted |
      | Declined |

  Scenario Outline: Forbids anyone but the current approver
    Given <owner> has a pending correction request
    And <actor> is signed in
    When <actor> accepts the request
    Then it is forbidden

    Examples:
      | owner | actor |
      | Eve   | Eve   |
      | Eve   | Ed    |
      | Eve   | Sue   |
      | Sam   | Sam   |
      | Sam   | Sue   |
