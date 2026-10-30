Feature: To-do badges
  Counts derived from stored state on each load. No notifications in v1.

  Background:
    Given it is Friday 2026-10-30 10:00

  Scenario: Employee to-dos
    Given Eve clocked in at 08:00 and has not clocked out
    And Sam returned Eve's timesheet for week 2026-10-12
    And Eve's timesheet for week 2026-10-19 is a Draft
    And Sam declined a correction request of Eve 3 days ago
    And Sam declined another one 8 days ago
    And Eve is signed in
    When Eve loads the to-dos
    Then Eve sees: clocked in since 08:00, 1 returned timesheet, 1 unsubmitted past week, 1 request declined in the last 7 days

  Scenario: Supervisor to-dos come from the team
    Given Eve and Ed each have a Submitted timesheet
    And Ed has a pending correction request
    And Sam is signed in
    When Sam loads the to-dos
    Then Sam sees 2 timesheets to review and 1 correction request to decide
    And Sam also sees Sam's own employee to-dos

  Scenario: Admin to-dos come from supervisors
    Given Sam has a Submitted timesheet and Sue has a pending correction request
    And Ada is signed in
    When Ada loads the to-dos
    Then Ada sees 1 timesheet to review and 1 correction request to decide

  Scenario: Shows nothing when everything is done
    Given Ed has no open entry, no Draft past weeks and nothing returned or declined
    And Ed is signed in
    When Ed loads the to-dos
    Then every count is zero
