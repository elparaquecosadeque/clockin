Feature: Set a password with a link
  Anonymous. Used both to activate an account and to reset a password.

  Scenario: Activates a pending account
    Given "Fay" is PendingActivation with a valid link
    When the link is used with the password "correct horse battery"
    Then "Fay" is Active
    And "Fay" can sign in with that password
    And the link no longer works

  Scenario: Resets the password of an active account
    Given Eve has a valid reset link and two signed-in sessions
    When the link is used with the password "a brand new passphrase"
    Then Eve can sign in only with the new password
    And Eve's security stamp has changed
    And both earlier sessions are rejected on their next request

  Scenario: Clears a lockout
    Given Eve is locked out after 5 failed sign-ins
    And Eve has a valid reset link
    When the link is used with a valid new password
    Then Eve can sign in immediately

  Rule: Passwords are 12 to 128 characters, with no composition rules

    Scenario Outline: Checks password length
      Given "Fay" has a valid link
      When the link is used with a password of <length> characters
      Then the result is <result>

      Examples:
        | length | result                         |
        | 11     | a validation error on password |
        | 12     | success                        |
        | 128    | success                        |
        | 129    | a validation error on password |

    Scenario: Accepts a password with only lowercase letters and spaces
      Given "Fay" has a valid link
      When the link is used with the password "only lower case words"
      Then "Fay" is Active

  Rule: A link is single-use, expires after 72 hours, and is replaced by a newer one
    Every failure gives the same answer, so a link reveals nothing about the account.

    Scenario Outline: Rejects an unusable link
      Given <situation>
      When the link is used with a valid password
      Then it breaks the rule "InvalidOrExpiredLink"

      Examples:
        | situation                                             |
        | the link was generated 72 hours and 1 second ago      |
        | the link has already been used                        |
        | a newer link was generated for the same user          |
        | the token does not exist                              |
        | the token is malformed                                |
        | the user was deactivated after the link was generated |

    Scenario: Accepts a link just before it expires
      Given the link was generated 71 hours and 59 minutes ago
      When the link is used with a valid password
      Then the password is set
