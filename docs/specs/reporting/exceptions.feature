Feature: Exceptions
  Things a supervisor or admin should look at. Thresholds are domain constants.

  Background:
    Given it is Friday 2026-10-30 10:00
    And Sam is signed in

  @api
  Scenario: Pages matching exceptions without hiding old unresolved weeks
    Given the supervisor has 52 unresolved overdue weeks in scope
    When the supervisor requests the first page of exceptions
    Then it contains the newest 50 matching exceptions and indicates another page
    And the second page contains the remaining 2 without duplicates
    And invalid page numbers return 400 Bad Request

  Scenario: Lists overdue Drafts from ended weeks
    Given Eve's timesheet for week 2026-10-19 is a Draft
    And Ed's timesheet for week 2026-10-19 is Submitted
    When Sam views the exceptions
    Then Eve's week 2026-10-19 is listed as "Overdue draft"
    And Ed's week is not listed

  Scenario: Does not list the current week as overdue
    Given Eve's timesheet for week 2026-10-26 is a Draft
    When Sam views the exceptions
    Then week 2026-10-26 is not listed

  Scenario: Lists a returned timesheet that has not been resubmitted
    Given Sam returned Eve's timesheet for week 2026-10-12
    When Sam views the exceptions
    Then Eve's week 2026-10-12 is listed as "Overdue draft"

  Scenario Outline: Lists long entries
    Given Eve has an entry lasting <length> in week 2026-10-19
    When Sam views the exceptions
    Then that entry is <listed> as "Long entry"

    Examples:
      | length | listed     |
      | 10:00  | not listed |
      | 10:01  | listed     |

  Scenario Outline: Lists timesheets with many manual changes
    Given Eve's timesheet for week 2026-10-19 has <count> manually added, edited or deleted entries
    When Sam views the exceptions
    Then it is <listed> as "Many manual changes"

    Examples:
      | count | listed     |
      | 2     | not listed |
      | 3     | listed     |

  Scenario: Shows only the supervisor's team
    Given Olga's timesheet for week 2026-10-19 is a Draft
    When Sam views the exceptions
    Then Olga is not listed

  Scenario: Admins see every team and the supervisors
    Given Sam's timesheet for week 2026-10-19 is a Draft
    And Ada is signed in
    When Ada views the exceptions
    Then Sam's week 2026-10-19 is listed as "Overdue draft"
