Feature: Submit a timesheet
  Draft -> Submitted. The timesheet then waits in the approver's queue:
  the team supervisor for employees, any admin for supervisors.

  Background:
    Given it is Monday 2026-11-02 09:00

  Scenario: Submits an ended week
    Given Eve is signed in
    And Eve's timesheet for week 2026-10-26 is a Draft with 40 hours
    When Eve submits the timesheet for week 2026-10-26
    Then it is Submitted
    And it is in Sam's queue
    And the history records "Submitted" by Eve at 2026-11-02 09:00

  Scenario: Submits a week with no entries
    Given Eve is signed in
    And Eve has no timesheet for week 2026-10-26
    When Eve submits the timesheet for week 2026-10-26
    Then a Submitted timesheet for week 2026-10-26 with 0 hours exists

  Scenario: Sends a supervisor's timesheet to the admin queue
    Given Sam is signed in
    When Sam submits the timesheet for week 2026-10-26
    Then it is in the admin queue

  Scenario: Resubmits a returned timesheet
    Given Eve is signed in
    And Sam returned Eve's timesheet for week 2026-10-26 with comment "Tuesday is missing"
    When Eve submits the timesheet for week 2026-10-26
    Then it is Submitted
    And the history shows Submitted, Returned, Submitted in order

  Rule: Only an ended week can be submitted

    Scenario Outline: Checks the week has ended in company time
      Given Eve is signed in
      And it is <now>
      When Eve submits the timesheet for week 2026-10-26
      Then the result is <result>

      Examples:
        | now                        | result                           |
        | Sunday 2026-11-01 23:59:59 | it breaks the rule "WeekNotEnded" |
        | Monday 2026-11-02 00:00:00 | the timesheet is Submitted       |

    Scenario: Rejects a week that is not a Monday
      Given Eve is signed in
      When Eve submits the timesheet for week 2026-10-27
      Then it fails validation on "weekStart"

  Rule: A timesheet with an open entry cannot be submitted

    Scenario: Rejects an open entry in the week
      Given Eve is signed in
      And Eve clocked in on Sunday 2026-11-01 22:00 and has not clocked out
      When Eve submits the timesheet for week 2026-10-26
      Then it breaks the rule "OpenEntryInTimesheet"

    Scenario: Allows submitting while clocked in to a later week
      Given Eve is signed in
      And Eve clocked in at 2026-11-02 08:00 and has not clocked out
      When Eve submits the timesheet for week 2026-10-26
      Then it is Submitted

  Rule: Only a Draft can be submitted

    Scenario Outline: Rejects a timesheet that is not a Draft
      Given Eve is signed in
      And Eve's timesheet for week 2026-10-26 is <status>
      When Eve submits the timesheet for week 2026-10-26
      Then it breaks the rule "TimesheetNotEditable"

      Examples:
        | status    |
        | Submitted |
        | Approved  |

  Rule: People submit their own timesheets; admins may submit for anyone

    Scenario: Lets an admin submit on behalf of a deactivated employee
      Given Ed is deactivated with a Draft timesheet for week 2026-10-26
      And Ada is signed in
      When Ada submits Ed's timesheet for week 2026-10-26
      Then it is Submitted and in Sam's queue
      And the history records "Submitted" by Ada on behalf of Ed

    Scenario: Forbids a supervisor from submitting for a team member
      Given Sam is signed in
      When Sam submits Eve's timesheet for week 2026-10-26
      Then it is forbidden

    Scenario: Forbids admins from having timesheets of their own
      Given Ada is signed in
      When Ada submits a timesheet of Ada's own for week 2026-10-26
      Then it is forbidden
