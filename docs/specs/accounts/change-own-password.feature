Feature: Change own password

  Scenario: Changes the password and signs out other sessions
    Given Eve is signed in on a phone and a laptop
    When Eve changes the password from the phone, giving the current one
    Then Eve can sign in only with the new password
    And the laptop session is rejected on its next request
    And the phone session receives new tokens and stays signed in

  Scenario: Rejects a wrong current password
    When Eve changes the password giving a wrong current password
    Then it breaks the rule "InvalidCurrentPassword"
    And the password is unchanged

  Scenario: Applies the password policy
    When Eve changes the password to one of 11 characters
    Then it fails validation on "newPassword"

  Scenario: Works for admins too
    When Ada changes the password giving the current one
    Then Ada can sign in only with the new password
