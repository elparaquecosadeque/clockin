Feature: Clock in
  Employees and supervisors punch in. Admins do not track time.

  Scenario: Opens an entry at the current time
    Given Eve is signed in
    And it is Friday 2026-10-30 08:00:00
    When Eve clocks in from IP "203.0.113.7" with user agent "Mobile Safari"
    Then Eve has an open entry started at 2026-10-30 08:00:00
    And the entry records IP "203.0.113.7" and user agent "Mobile Safari" for the clock-in
    And the entry is not flagged as manually edited
    And the entry belongs to Eve's timesheet for week 2026-10-26

  Scenario: Creates the week's timesheet as a Draft on the first punch
    Given Eve has no timesheet for week 2026-10-26
    When Eve clocks in
    Then Eve has a Draft timesheet for week 2026-10-26

  Scenario: Lets a supervisor clock in
    Given Sam is signed in
    When Sam clocks in
    Then Sam has an open entry

  Scenario: Forbids admins
    Given Ada is signed in
    When Ada clocks in
    Then it is forbidden

  Rule: At most one open entry per person

    Scenario: Rejects a second clock-in
      Given Eve clocked in at 08:00 and has not clocked out
      When Eve clocks in
      Then it breaks the rule "EntryAlreadyOpen"

    Scenario: Rejects a clock-in while last week's entry is still open
      Given Eve clocked in on Sunday 2026-10-25 22:00 and never clocked out
      When Eve clocks in
      Then it breaks the rule "EntryAlreadyOpen"

    Scenario: Allows clocking in again after clocking out
      Given Eve clocked in at 08:00 and clocked out at 09:00
      And it is 09:30
      When Eve clocks in
      Then Eve has an open entry started at 09:30

    Scenario: Allows clocking in at the same second the last entry ended
      Given Eve has an entry from 08:00:00 to 09:00:00
      And it is 09:00:00
      When Eve clocks in
      Then Eve has an open entry started at 09:00:00

    @api
    Scenario: Two simultaneous clock-ins open only one entry
      When Eve clocks in from two devices at the same moment
      Then exactly one entry is open
      And the other request conflicts with "EntryAlreadyOpen"

  Rule: An entry belongs to the week, in company time, in which it started

    Scenario Outline: Picks the week from the company-time start
      Given it is <now>
      When Eve clocks in
      Then the entry belongs to week <week>

      Examples:
        | now                          | week       |
        | Sunday 2026-11-01 23:59:59   | 2026-10-26 |
        | Monday 2026-11-02 00:00:00   | 2026-11-02 |
        | 2026-11-02T04:59:59Z         | 2026-10-26 |
        | 2026-11-02T05:00:00Z         | 2026-11-02 |
