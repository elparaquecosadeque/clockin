Feature: Generate an access link
  An admin generates a one-time link and shares it by hand. For a pending user
  it is an activation link; for an active user it is a password reset link.

  Background:
    Given Ada is signed in

  Scenario: Generates a new activation link for a pending user
    Given "Fay" is PendingActivation with an earlier activation link
    When Ada generates an access link for "Fay"
    Then Ada receives a new link valid for 72 hours
    And the earlier link no longer works

  Scenario: Generates a reset link for an active user
    When Ada generates an access link for "Eve"
    Then Ada receives a link valid for 72 hours
    And Eve's current password still works until the link is used
    And Eve's sessions are not revoked

  Scenario: Rejects a deactivated user
    Given Ed is deactivated
    When Ada generates an access link for "Ed"
    Then it breaks the rule "UserIsDeactivated"

  Scenario: Writes an audit entry without the token
    When Ada generates an access link for "Eve"
    Then the Audit log records that Ada generated a link for Eve
    And the log entry does not contain the token

  Scenario: Rejects an unknown user
    When Ada generates an access link for an unknown user
    Then it is not found

  Scenario Outline: Forbids non-admins
    Given <user> is signed in
    When <user> generates an access link for "Eve"
    Then it is forbidden

    Examples:
      | user |
      | Sam  |
      | Eve  |
