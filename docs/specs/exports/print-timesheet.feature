@e2e
Feature: Print a timesheet or team week
  PDFs come from the browser: Print, then "Save as PDF". No PDF library in v1.

  Scenario: Prints a timesheet without the app chrome
    Given Eve is viewing the timesheet for week 2026-10-19
    When Eve opens the print view
    Then the page shows the name, team, week, status, entries, daily totals and week total
    And navigation, buttons and badges are hidden in print media
    And the browser print dialog opens

  Scenario: Prints a supervisor's team week
    Given Sam is viewing the team grid for week 2026-10-19
    When Sam opens the print view
    Then the grid fits the page width in landscape

  Scenario: Uses the viewer's language and time format
    Given Eve's language is "es" and time format is "12h"
    When Eve opens the print view
    Then labels are in Spanish and times look like "8:00 a. m."

  Scenario: Passes an accessibility check
    When the print view is checked with axe
    Then there are no violations
