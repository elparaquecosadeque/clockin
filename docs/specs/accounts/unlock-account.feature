Feature: Unlock an account

  Scenario: Lets a locked user sign in again
    Given Ada is signed in
    And Eve is locked out
    When Ada unlocks Eve
    Then Eve can sign in with the correct password
    And Eve's failure counter is zero

  Scenario: Accepts unlocking a user who is not locked
    Given Ada is signed in
    When Ada unlocks Eve
    Then nothing changes

  Scenario: Forbids non-admins
    Given Sam is signed in
    When Sam unlocks Eve
    Then it is forbidden
