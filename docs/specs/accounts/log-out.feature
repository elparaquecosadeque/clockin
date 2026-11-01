Feature: Log out

  Scenario: Revokes the refresh token and clears the cookie
    Given Eve is signed in
    When Eve logs out
    Then Eve's refresh token no longer works
    And Eve's access token is rejected on its next request
    And the refresh cookie is cleared

  Scenario: Leaves other sessions signed in
    Given Eve is signed in on a phone and a laptop
    When Eve logs out on the phone
    Then the laptop session can still refresh
