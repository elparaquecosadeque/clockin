@api
Feature: Create and activate accounts through SecondPublicApi
  The development host reuses the existing account and permission rules.

  Scenario: Public information and authenticated identity
    When I request the public information endpoint without a token
    Then I receive only the API name and version
    When I request my identity without a token
    Then the response is 401
    When I log in as Mia and request my identity
    Then I receive Mia's identity and current permissions

  Scenario: An admin creates and activates a supervisor
    Given I have logged in as Ada
    When I create a supervisor without a team
    Then I receive an activation token
    And the new user cannot log in before activation
    When I set a valid password using that activation token
    Then the user can log in and read their identity
    And the activation token cannot be reused
    And the supervisor receives 403 when trying to create a user

  Scenario: An employee requires an active team
    Given I have logged in as Ada
    When I create an employee without a teamId
    Then the response is 400 with a teamId field error
    When I use the Office teamId from the Scalar example
    Then the employee is created and can activate and log in

  Scenario: Existing validation and authentication are preserved
    Then duplicate email creation returns 409
    And invalid activation tokens are rejected
    And login and activation requests are rate limited
    And expired tokens and revoked sessions are rejected
    And tokens from the other hosts are rejected

  Scenario: Development isolation
    Then SecondPublicApi refuses to start outside Development
    And it never creates, migrates, seeds or resets the shared database
    And Scalar preloads Ada and Mia credentials and supervisor and Office employee examples
