Feature: Rename a team

  Background:
    Given Ada is signed in

  Scenario: Renames a team
    When Ada renames team "Warehouse" to "Logistics"
    Then team "Logistics" has the same employees and supervisor

  Scenario: Allows changing only the case
    When Ada renames team "Warehouse" to "WAREHOUSE"
    Then the team is named "WAREHOUSE"

  Scenario: Rejects a name used by another team
    When Ada renames team "Warehouse" to "office"
    Then it conflicts with "TeamNameInUse"

  Scenario: Rejects renaming an archived team
    Given team "Old Store" is archived
    When Ada renames team "Old Store" to "Older Store"
    Then it breaks the rule "TeamIsArchived"

  Scenario: Forbids non-admins
    Given Sam is signed in
    When Sam renames team "Warehouse" to "Logistics"
    Then it is forbidden
