Feature: Approve a timesheet
  Submitted -> Approved. Approved is final for employees and supervisors.

  Scenario: The team supervisor approves an employee's timesheet
    Given Eve's timesheet for week 2026-10-19 is Submitted
    And Sam is signed in
    When Sam approves it
    Then it is Approved by Sam
    And the history records "Approved" by Sam
    And none of its entries can be added, edited or deleted

  Scenario: An admin approves a supervisor's timesheet
    Given Sam's timesheet for week 2026-10-19 is Submitted
    And Ada is signed in
    When Ada approves it
    Then it is Approved by Ada

  Scenario: An admin approves an employee's timesheet
    Given Eve's timesheet for week 2026-10-19 is Submitted
    And Ada is signed in
    When Ada approves it
    Then it is Approved by Ada

  Scenario: Approves a deactivated employee's timesheet
    Given Ed is deactivated and Ed's timesheet for week 2026-10-19 is Submitted
    And Sam is signed in
    When Sam approves it
    Then it is Approved by Sam

  Rule: Only a Submitted timesheet without a pending correction request can be approved

    Scenario Outline: Rejects a timesheet in the wrong state
      Given Eve's timesheet for week 2026-10-19 is <status>
      And Sam is signed in
      When Sam approves it
      Then it breaks the rule "TimesheetNotSubmitted"

      Examples:
        | status   |
        | Draft    |
        | Approved |

    Scenario: Rejects a timesheet with a pending correction request
      Given Eve's timesheet for week 2026-10-19 is Submitted
      And Eve has a pending correction request on it
      And Sam is signed in
      When Sam approves it
      Then it breaks the rule "PendingCorrectionRequest"

  Rule: Only the current approver acts, and never on their own timesheet

    Scenario Outline: Forbids anyone else
      Given <owner>'s timesheet for week 2026-10-19 is Submitted
      And <actor> is signed in
      When <actor> approves it
      Then it is forbidden

      Examples:
        | owner | actor |
        | Olga  | Sam   |
        | Eve   | Ed    |
        | Eve   | Eve   |
        | Sam   | Sam   |
        | Sam   | Sue   |

    Scenario: Follows the employee to the new team
      Given Eve's timesheet for week 2026-10-19 is Submitted
      And Ada moved Eve to team "Office"
      When Sam approves it
      Then it is forbidden
      But Sue can approve it

    @api
    Scenario: Two approvers at the same moment
      Given Eve's timesheet for week 2026-10-19 is Submitted
      When Sam and Ada approve it at the same moment
      Then exactly one approval is recorded
      And the other request conflicts with "ConcurrencyConflict"
