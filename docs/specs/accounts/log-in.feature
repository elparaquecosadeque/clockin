Feature: Log in
  Anonymous. Email and password give a 15-minute access token and a 12-hour refresh cookie.

  @api
  Scenario: Rejects an oversized email before it reaches the audit log
    When someone signs in with an email longer than 254 characters
    Then the response is 400 Bad Request with an email validation error
    And the sign-in handler does not run

  Scenario: Signs in an active user
    When Eve signs in with the correct email and password
    Then Eve receives an access token valid for 15 minutes carrying sub, sid (the session) and security_stamp only
    And a refresh token in an HttpOnly, Secure, SameSite=Strict cookie valid for 12 hours
    And the Audit log records a successful sign-in for Eve

  Scenario: Matches the email ignoring case
    When Eve signs in with "EVE@ClockIn.Test" and the correct password
    Then Eve is signed in

  Rule: Every failure gives the same generic answer

    Scenario Outline: Rejects bad credentials without revealing why
      Given <situation>
      When someone signs in as "<email>"
      Then it breaks the rule "InvalidCredentials"
      And the Audit log records a failed sign-in without the password

      Examples:
        | situation                | email               |
        | the password is wrong    | eve@clockin.test    |
        | no user has that email   | nobody@clockin.test |
        | Fay is PendingActivation | fay@clockin.test    |
        | Ed is deactivated        | ed@clockin.test     |
        | Eve is locked out        | eve@clockin.test    |

    Scenario: Gives the same answer when another sign-in for the same user saves first
      Given Eve is signing in with a wrong password
      And another sign-in for Eve saves first
      When Eve's attempt is saved
      Then it breaks the rule "InvalidCredentials", not a concurrency conflict
      # A 409 only a registered email could produce would reveal which emails exist.

    Scenario: Logs the email as one line whatever it contains
      When someone signs in as "nobody@clockin.test" followed by a line break and a fake log entry
      Then the Audit log records one failed sign-in line, with the control characters replaced by "?"

  Rule: 5 consecutive failures lock the account for 15 minutes

    Scenario: Locks on the fifth consecutive failure
      Given Eve has failed to sign in 4 times in a row
      When Eve signs in with a wrong password
      Then Eve is locked out until 15 minutes from now
      And the Audit log records the lockout

    Scenario: Stays locked even with the correct password
      Given Eve was locked out 14 minutes and 59 seconds ago
      When Eve signs in with the correct password
      Then it breaks the rule "InvalidCredentials"

    Scenario: Unlocks after 15 minutes
      Given Eve was locked out 15 minutes ago
      When Eve signs in with the correct password
      Then Eve is signed in

    Scenario: A success resets the counter
      Given Eve has failed 4 times, then signed in successfully, then failed 4 more times
      When Eve signs in with the correct password
      Then Eve is signed in

  @api
  Scenario: Rate-limits sign-in attempts per client
    Given a client has reached the sign-in rate limit
    When it tries to sign in again
    Then the response is 429 Too Many Requests
