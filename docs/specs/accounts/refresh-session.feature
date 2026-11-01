Feature: Refresh a session
  The refresh token slides: each use issues a new one valid for 12 hours.

  Scenario: Issues a new access token and rotates the refresh token
    Given Eve's refresh token was issued 11 hours ago
    When Eve refreshes the session
    Then Eve receives a new access token
    And a new refresh token valid for 12 hours from now
    And the old refresh token no longer works

  Scenario: Keeps a worker signed in for a long shift
    Given Eve refreshes the session every 15 minutes for 20 hours
    Then every refresh succeeds

  Scenario: Two tabs refresh at the same moment
    Given Eve has the app open in two tabs and her access token has expired
    When both tabs refresh the session at the same moment
    Then the second tab waits for the first and refreshes with the new refresh token
    And Eve stays signed in on every device

  Scenario: Rejects an expired refresh token
    Given Eve's refresh token was issued 12 hours and 1 second ago
    When Eve refreshes the session
    Then it is unauthorized

  Scenario: Detects reuse of a rotated refresh token
    Given Eve's refresh token has already been rotated
    When the old refresh token is used again
    Then it is unauthorized
    And every refresh token of Eve is revoked
    And the Audit log records a refresh-token reuse for Eve

  Scenario Outline: Rejects a session whose security stamp changed
    Given <change>
    When Eve refreshes the session
    Then it is unauthorized

    Examples:
      | change                   |
      | Eve's password was reset |
      | Eve was deactivated      |
      | Eve's role was changed   |
