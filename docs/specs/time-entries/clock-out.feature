Feature: Clock out

  Background:
    Given Eve is signed in

  Scenario: Closes the open entry at the current time
    Given Eve clocked in at 08:00:00
    And it is 16:30:00
    When Eve clocks out from IP "203.0.113.7" with user agent "Mobile Safari"
    Then the entry runs from 08:00:00 to 16:30:00
    And the entry records IP "203.0.113.7" and user agent "Mobile Safari" for the clock-out
    And Eve has no open entry

  Scenario: Rejects a clock-out with no open entry
    When Eve clocks out
    Then it breaks the rule "NoOpenEntry"

  Scenario: Keeps an entry that crosses midnight on Sunday in the week it started
    Given Eve clocked in on Sunday 2026-11-01 22:00
    And it is Monday 2026-11-02 06:00
    When Eve clocks out
    Then the 8-hour entry belongs to week 2026-10-26
    And week 2026-11-02 has no hours from it

  Scenario: Closes an entry left open from last week
    Given Eve clocked in on Sunday 2026-10-25 20:00
    And it is Monday 2026-10-26 04:00
    When Eve clocks out
    Then the entry belongs to week 2026-10-19

  Rule: An entry lasts more than zero and at most 16 hours

    Scenario Outline: Checks the length at clock-out
      Given Eve clocked in at 2026-10-29 18:00:00
      And it is <now>
      When Eve clocks out
      Then the result is <result>

      Examples:
        | now                 | result                                |
        | 2026-10-29 18:00:00 | it breaks the rule "EntryHasNoLength" |
        | 2026-10-29 18:00:01 | the entry is closed                   |
        | 2026-10-30 10:00:00 | the entry is closed                   |
        | 2026-10-30 10:00:01 | it breaks the rule "EntryTooLong"     |

    Scenario: Leaves a too-long entry open so it can be fixed by hand
      Given Eve clocked in yesterday at 08:00 and forgot to clock out
      When Eve clocks out at 10:00 today
      Then it breaks the rule "EntryTooLong"
      And the entry is still open
