Feature: Export approved hours as CSV
  One row per entry of an Approved timesheet. Meant for payroll, so the format
  does not depend on the user's language.

  Background:
    Given Sam is signed in
    And Eve's timesheet for week 2026-10-19 is Approved
    And Ed's timesheet for week 2026-10-19 is Submitted

  Scenario: Exports the team's approved entries for a week
    When Sam exports week 2026-10-19
    Then the file is UTF-8 with a byte order mark, comma-separated, CRLF line endings
    And the header is "Employee,Email,Team,WeekStart,Date,ClockIn,ClockOut,Hours,HoursDecimal,ManuallyChanged,Note"
    And it has one row per entry of Eve's timesheet
    And it has no rows for Ed

  Scenario: Writes times in company time and dates as ISO
    Given Eve's entry ran from 2026-10-20T13:00:00Z to 2026-10-20T21:45:00Z
    When Sam exports week 2026-10-19
    Then that row has Date "2026-10-20", ClockIn "08:00", ClockOut "16:45"

  Rule: Hours are shown both ways, rounded from the exact seconds

    Scenario Outline: Formats hours
      Given an entry lasts <duration>
      When Sam exports week 2026-10-19
      Then that row has Hours "<hours>" and HoursDecimal "<decimal>"

      Examples:
        | duration | hours | decimal |
        | 7:45:00  | 7:45  | 7.75    |
        | 7:44:30  | 7:45  | 7.74    |
        | 0:00:29  | 0:00  | 0.01    |
        | 16:00:00 | 16:00 | 16.00   |

    Scenario: Uses a dot for decimals whatever the user's language
      Given Sam's language is "es"
      When Sam exports week 2026-10-19
      Then decimals use "." and headers are in English

  Rule: Text is escaped safely

    Scenario Outline: Escapes notes
      Given an entry has note <note>
      When Sam exports week 2026-10-19
      Then the Note cell is <cell>

      Examples:
        | note                  | cell                       |
        | "client, on site"     | "\"client, on site\""      |
        | "said \"hi\""         | "\"said \"\"hi\"\"\""      |
        | "two\nlines"          | "\"two\nlines\""           |
        | "=HYPERLINK(\"x\")"   | "\"'=HYPERLINK(\"\"x\"\")\"" |
        | "+1 hour"             | "'+1 hour"                 |
        | "-1 hour"             | "'-1 hour"                 |
        | "@sum"                | "'@sum"                    |

  Scenario: Produces only a header when nothing is approved
    Given no timesheet of team "Warehouse" for week 2026-10-12 is Approved
    When Sam exports week 2026-10-12
    Then the file contains only the header

  Scenario: Keeps the team name the timesheet had when it was approved
    Given Eve moved to team "Office" after the approval
    When Ada exports week 2026-10-19
    Then Eve's rows show team "Warehouse"

  Rule: Supervisors export their team; admins export everything

    Scenario: Admins export all teams, supervisors included
      Given Sam's timesheet for week 2026-10-19 is Approved
      And Ada is signed in
      When Ada exports week 2026-10-19
      Then it has rows for Eve and for Sam, with Sam's Team empty

    Scenario: Admins filter by team
      Given Ada is signed in
      When Ada exports week 2026-10-19 for team "Office"
      Then it has rows for team "Office" only

    Scenario Outline: Forbids exporting outside one's scope
      Given <user> is signed in
      When <user> exports week 2026-10-19 for team "<team>"
      Then it is forbidden

      Examples:
        | user | team      |
        | Sam  | Office    |
        | Eve  | Warehouse |
