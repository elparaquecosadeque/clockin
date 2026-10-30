Feature: Reactivate a user

  Background:
    Given Ada is signed in
    And Ed is deactivated

  Scenario: Reactivates a user with a fresh activation link
    When Ada reactivates Ed
    Then Ed has status PendingActivation
    And Ada receives an activation link valid for 72 hours
    And Ed's old password no longer works

  Scenario: Keeps the user's history
    Given Ed has Approved timesheets from before the deactivation
    When Ada reactivates Ed
    Then Ed's Approved timesheets are unchanged

  Scenario: Requires an active team when the old team was archived
    Given Ed's team "Warehouse" has since been archived
    When Ada reactivates Ed without choosing a team
    Then it breaks the rule "TeamIsArchived"

  Scenario: Moves the user to the chosen team
    Given Ed's team "Warehouse" has since been archived
    When Ada reactivates Ed in team "Office"
    Then Ed is in team "Office"

  Scenario: Rejects a user who is not deactivated
    When Ada reactivates Eve
    Then it breaks the rule "UserIsNotDeactivated"

  Scenario: Forbids non-admins
    Given Sam is signed in
    When Sam reactivates Ed
    Then it is forbidden
