Feature: Seed the first admin
  On deploy, the system creates the first admin so somebody can create everyone else.

  Scenario: Creates the first admin on an empty system
    Given no users exist
    And the deploy config sets the first admin email to "admin@clockin.test"
    When the application starts
    Then an Admin "admin@clockin.test" exists with status PendingActivation
    And a one-time activation link for that admin is written to the log

  Scenario: Does nothing when an admin already exists
    Given an active admin exists
    When the application starts
    Then no user is created
    And no activation link is written to the log

  Scenario: Issues a fresh link when the seeded admin has not activated yet
    Given the seeded admin "admin@clockin.test" is still PendingActivation
    When the application restarts
    Then a new activation link is written to the log
    And the previous activation link no longer works

  Scenario: Fails to start when there is no admin and no email configured
    Given no users exist
    And the deploy config has no first admin email
    When the application starts
    Then startup fails with a configuration error naming the missing setting

  Scenario: Ignores the configured email once any admin exists
    Given an active admin exists
    And the deploy config sets the first admin email to "someone-else@clockin.test"
    When the application starts
    Then no user is created
