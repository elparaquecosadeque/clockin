Feature: Add a manual entry
  An employee adds a forgotten entry to a Draft week. It is flagged as manual.

  Background:
    Given Eve is signed in
    And it is Friday 2026-10-30 10:00

  Scenario: Adds a flagged entry with a note
    When Eve adds an entry on 2026-10-29 from 08:00 to 12:00 with note "forgot my phone"
    Then the entry is flagged as manually added by Eve with note "forgot my phone"
    And it belongs to Eve's timesheet for week 2026-10-26
    And the timesheet history records the addition

  Scenario: Creates a Draft timesheet for a past week that had none
    Given Eve has no timesheet for week 2026-10-12
    When Eve adds an entry on 2026-10-14 from 08:00 to 12:00 with a note
    Then Eve has a Draft timesheet for week 2026-10-12

  Scenario: Allows any past week while it is a Draft
    Given Eve's timesheet for week 2025-10-27 is a Draft
    When Eve adds an entry on 2025-10-28 from 08:00 to 12:00 with a note
    Then the entry is added

  Scenario Outline: Rejects a missing or overlong note
    When Eve adds an entry on 2026-10-29 from 08:00 to 12:00 with note "<note>"
    Then it fails validation on "note"

    Examples:
      | note      |
      |           |
      | "   "     |
      | 501 chars |

  Rule: Entries never lie in the future

    Scenario Outline: Checks the end against now
      When Eve adds an entry on 2026-10-30 from 08:00 to <end> with a note
      Then the result is <result>

      Examples:
        | end      | result                             |
        | 10:00:00 | the entry is added                 |
        | 10:00:01 | it breaks the rule "EntryInFuture" |

  Rule: An entry lasts more than zero and at most 16 hours

    Scenario Outline: Checks the length
      When Eve adds an entry from <start> to <end> with a note
      Then the result is <result>

      Examples:
        | start            | end              | result                                |
        | 2026-10-29 09:00 | 2026-10-29 09:00 | it breaks the rule "EntryHasNoLength" |
        | 2026-10-29 09:00 | 2026-10-29 08:00 | it breaks the rule "EntryHasNoLength" |
        | 2026-10-28 08:00 | 2026-10-29 00:00 | the entry is added                    |
        | 2026-10-28 08:00 | 2026-10-29 00:01 | it breaks the rule "EntryTooLong"     |

  Rule: Entries never overlap, but may touch

    Background:
      Given Eve has an entry on 2026-10-29 from 09:00 to 12:00

    Scenario Outline: Checks the new entry against the existing one
      When Eve adds an entry on 2026-10-29 from <start> to <end> with a note
      Then the result is <result>

      Examples:
        | start | end   | result                                |
        | 08:00 | 09:30 | it breaks the rule "EntriesOverlap"   |
        | 11:30 | 13:00 | it breaks the rule "EntriesOverlap"   |
        | 10:00 | 11:00 | it breaks the rule "EntriesOverlap"   |
        | 08:00 | 13:00 | it breaks the rule "EntriesOverlap"   |
        | 09:00 | 12:00 | it breaks the rule "EntriesOverlap"   |
        | 08:00 | 09:00 | the entry is added                    |
        | 12:00 | 13:00 | the entry is added                    |

    Scenario: Treats an open entry as running until now
      Given Eve clocked in at 2026-10-30 08:00 and has not clocked out
      When Eve adds an entry on 2026-10-30 from 09:00 to 09:30 with a note
      Then it breaks the rule "EntriesOverlap"

    Scenario: Checks overlaps across a week boundary
      Given Eve has an entry from Sunday 2026-10-25 22:00 to Monday 2026-10-26 02:00
      When Eve adds an entry on Monday 2026-10-26 from 01:00 to 03:00 with a note
      Then it breaks the rule "EntriesOverlap"

    Scenario: Two entries for the same person at the same moment, either side of a week boundary
      When Eve adds an entry from Sunday 2026-10-25 22:00 to Monday 2026-10-26 02:00
      And at the same moment Ada adds an entry for Eve on Monday 2026-10-26 from 01:00 to 03:00
      Then the second waits for the first to finish
      And it breaks the rule "EntriesOverlap"

  Rule: Only a Draft timesheet accepts changes

    Scenario Outline: Rejects a week that is not a Draft
      Given Eve's timesheet for week 2026-10-19 is <status>
      When Eve adds an entry on 2026-10-20 from 08:00 to 12:00 with a note
      Then it breaks the rule "TimesheetNotEditable"

      Examples:
        | status    |
        | Submitted |
        | Approved  |

    Scenario: Allows a returned timesheet, which is a Draft again
      Given Sam returned Eve's timesheet for week 2026-10-19
      When Eve adds an entry on 2026-10-20 from 08:00 to 12:00 with a note
      Then the entry is added

  Rule: Employees change only their own time; admins change anyone's Draft with a reason

    Scenario: Forbids a supervisor from adding to a team member's week
      Given Sam is signed in
      When Sam adds an entry for Eve
      Then it is forbidden

    Scenario: Lets an admin add to any Draft, flagged as an admin edit
      Given Ada is signed in
      When Ada adds an entry for Eve on 2026-10-29 from 08:00 to 12:00 with reason "badge reader down"
      Then the entry is flagged as manually added by Ada with note "badge reader down"
