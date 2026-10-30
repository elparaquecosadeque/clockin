Feature: Edit an entry
  Every edit is flagged, needs a note, and keeps the originally punched values forever.

  Background:
    Given Eve is signed in
    And it is Friday 2026-10-30 10:00
    And Eve punched an entry on 2026-10-29 from 08:02 to 17:00

  Scenario: Changes the times and keeps the punched values
    When Eve edits the entry to 08:00 to 17:00 with note "badge was slow"
    Then the entry runs from 08:00 to 17:00
    And it is flagged as edited by Eve
    And it still shows the punched values 08:02 to 17:00
    And the history records before 08:02-17:00, after 08:00-17:00, editor Eve and the note

  Scenario: Keeps the punched values after several edits
    Given Eve edited the entry to 08:00 to 17:00
    When Eve edits the entry to 08:00 to 16:00 with a note
    Then it still shows the punched values 08:02 to 17:00
    And the history has two edit rows

  Scenario: Closes a forgotten open entry by setting its end
    Given Eve clocked in at 2026-10-30 07:00 and has not clocked out
    When Eve sets the open entry's end to 2026-10-30 09:30 with note "forgot to clock out"
    Then the entry runs from 07:00 to 09:30 and is flagged as edited
    And Eve has no open entry

  Scenario: Moves only the start of an open entry
    Given Eve clocked in at 2026-10-30 09:15 and has not clocked out
    When Eve sets the open entry's start to 08:45 with a note
    Then the entry is still open, starting at 08:45

  Scenario: Rejects an edit without a note
    When Eve edits the entry to 08:00 to 17:00 without a note
    Then it fails validation on "note"

  Rule: An entry stays in the week it started in

    Scenario Outline: Checks the new start against the week
      Given Eve has an entry on Sunday 2026-10-25 from 20:00 to 22:00
      When Eve edits its start to <start> and end to <end> with a note
      Then the result is <result>

      Examples:
        | start                     | end                       | result                                     |
        | Sunday 2026-10-25 23:00   | Monday 2026-10-26 01:00   | the entry is updated                       |
        | Monday 2026-10-26 00:00   | Monday 2026-10-26 01:00   | it breaks the rule "EntryCannotChangeWeek" |
        | Saturday 2026-10-17 20:00 | Saturday 2026-10-17 22:00 | it breaks the rule "EntryCannotChangeWeek" |

  Rule: The same rules as a new entry apply, and the entry never overlaps itself

    Scenario Outline: Rejects an invalid result
      Given Eve also has an entry on 2026-10-29 from 18:00 to 20:00
      When Eve edits the first entry to <start> to <end> with a note
      Then it breaks the rule "<code>"

      Examples:
        | start            | end              | code             |
        | 2026-10-29 08:00 | 2026-10-29 18:30 | EntriesOverlap   |
        | 2026-10-29 08:00 | 2026-10-29 08:00 | EntryHasNoLength |
        | 2026-10-29 08:00 | 2026-10-30 00:01 | EntryTooLong     |
        | 2026-10-30 08:00 | 2026-10-30 11:00 | EntryInFuture    |

    Scenario: Allows shifting an entry within its own time
      When Eve edits the entry to 08:30 to 16:30 with a note
      Then the entry is updated

  Rule: Only a Draft timesheet accepts changes

    Scenario: Rejects an edit in a Submitted week
      Given Eve submitted the timesheet for week 2026-10-19 that holds an entry
      When Eve edits that entry with a note
      Then it breaks the rule "TimesheetNotEditable"

  Rule: Employees edit only their own entries; admins edit anyone's Draft with a reason

    Scenario: Forbids editing someone else's entry
      Given Ed is signed in
      When Ed edits Eve's entry with a note
      Then it is forbidden

    Scenario: Forbids a supervisor
      Given Sam is signed in
      When Sam edits Eve's entry with a note
      Then it is forbidden

    Scenario: Lets an admin close an open entry during offboarding
      Given Eve clocked in at 2026-10-28 08:00 and never clocked out
      And Ada is signed in
      When Ada sets the entry's end to 2026-10-28 17:00 with reason "offboarding, last shift"
      Then the entry is closed and flagged as edited by Ada
      And Eve can now be deactivated

    Scenario: Rejects an unknown entry
      When Eve edits an entry that does not exist
      Then it is not found

  @api
  Scenario: Rejects an edit based on a stale version
    Given Eve opened the entry on two devices
    And Eve saved an edit on the first device
    When Eve saves a different edit on the second device
    Then it conflicts with "ConcurrencyConflict"
