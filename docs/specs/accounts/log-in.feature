Feature: Log in
  Anonymous. Email and password give a 15-minute access token and a 12-hour refresh cookie.

  Scenario: Signs in an active user
    When Eve signs in with the correct email and password
    Then Eve receives an access token valid for 15 minutes carrying sub and security_stamp only
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
