Feature: Delete an entry

  Background:
    Given Eve is signed in
    And Eve has an entry on 2026-10-29 from 08:00 to 12:00

  Scenario: Deletes an entry from a Draft week and keeps a snapshot
    When Eve deletes the entry with note "duplicate"
    Then the entry no longer counts toward the week
    And the history keeps the deleted entry's times, punched values, deleter and note

  Scenario: Deletes an accidental open entry so Eve can clock in again
    Given Eve clocked in at 2026-10-30 09:59 by mistake
    When Eve deletes the open entry with note "tapped twice"
    Then Eve has no open entry
    And Eve can clock in

  Scenario: Rejects a delete without a note
    When Eve deletes the entry without a note
    Then it fails validation on "note"

  Scenario: Rejects a delete in a week that is not a Draft
    Given Eve submitted the timesheet for week 2026-10-19 that holds an entry
    When Eve deletes that entry with a note
    Then it breaks the rule "TimesheetNotEditable"

  Scenario: Forbids deleting someone else's entry
    Given Ed is signed in
    When Ed deletes Eve's entry with a note
    Then it is forbidden
