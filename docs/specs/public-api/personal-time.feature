@public-api
Feature: Manage personal time through the development public API
  The versioned API exposes existing UI operations without changing their rules.

  Scenario: Sign in and manage my own time
    Given the shared development database has been prepared by the existing API
    When I sign in with an active employee account
    Then I receive a bearer token lasting 15 minutes and no refresh cookie
    When I punch in and later punch out
    Then the same entry is closed using server time
    When I read my week and edit an entry with its latest timesheet version and a note
    Then the original punches and edit history are preserved
    And another edit using the old version is rejected

  Scenario: Submit an ended week
    Given my ended week is Draft and has no open entry
    When I submit that week
    Then the week becomes Submitted
    And further entry changes are rejected

  Scenario: Preserve validation and ownership
    Then requests without authentication are rejected
    And admins cannot punch or edit another person's entries through this API
    And employees cannot edit another person's entries
    And invalid notes and non-Monday week dates are rejected with field errors
    And duplicate punches, overlapping entries and submitting the current week are rejected

  Scenario: Isolate the development host
    Then the public API refuses to start outside Development
    And it does not migrate, seed or reset the database
    And the SPA API's tokens are not accepted by the public API
    And the public API's tokens are not accepted by the SPA API
