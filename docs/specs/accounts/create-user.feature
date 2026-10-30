Feature: Create a user
  Admins create every account. There is no self-signup.

  Scenario: Creates an employee in a team
    Given Ada is signed in
    When Ada creates the employee "Fay" with email "fay@clockin.test" in team "Warehouse"
    Then "Fay" exists with role Employee, team "Warehouse" and status PendingActivation
    And Ada receives a one-time activation link valid for 72 hours
    And only a hash of the link token is stored
    And "Fay" has language "en" and time format "24h"

  Scenario: Creates a user with the given preferences
    Given Ada is signed in
    When Ada creates the employee "Fay" in team "Warehouse" with language "es" and time format "12h"
    Then "Fay" has language "es" and time format "12h"

  Scenario: Creates a supervisor without a team
    Given Ada is signed in
    When Ada creates the supervisor "Sid"
    Then "Sid" exists with role Supervisor, no team and status PendingActivation

  Scenario: Creates another admin
    Given Ada is signed in
    When Ada creates the admin "Amy"
    Then "Amy" exists with role Admin, no team and status PendingActivation

  Rule: Email is unique, ignoring case and surrounding spaces

    Scenario Outline: Rejects an email that is already taken
      Given a user with email "eve@clockin.test" exists
      When Ada creates the employee "Eve2" with email "<email>" in team "Warehouse"
      Then it conflicts with "EmailAlreadyInUse"

      Examples:
        | email                  |
        | eve@clockin.test       |
        | EVE@ClockIn.Test       |
        | "  eve@clockin.test  " |

    Scenario: Rejects an email taken by a deactivated user
      Given the deactivated user "Old" has email "old@clockin.test"
      When Ada creates the employee "New" with email "old@clockin.test" in team "Warehouse"
      Then it conflicts with "EmailAlreadyInUse"

    Scenario: Stores the email trimmed and lowercased
      When Ada creates the employee "Fay" with email " Fay@ClockIn.Test " in team "Warehouse"
      Then "Fay" has email "fay@clockin.test"

  Rule: An employee belongs to exactly one active team, and nobody else belongs to a team

    Scenario: Rejects an employee without a team
      When Ada creates the employee "Fay" without a team
      Then it fails validation on "teamId"

    Scenario Outline: Rejects a team for a supervisor or an admin
      When Ada creates the <role> "X" in team "Warehouse"
      Then it fails validation on "teamId"

      Examples:
        | role       |
        | supervisor |
        | admin      |

    Scenario: Rejects an archived team
      Given team "Old Store" is archived
      When Ada creates the employee "Fay" in team "Old Store"
      Then it breaks the rule "TeamIsArchived"

    Scenario: Rejects a team that does not exist
      When Ada creates the employee "Fay" in an unknown team
      Then it is not found

  Rule: Input is validated

    Scenario Outline: Rejects invalid input
      When Ada creates a user with <field> set to "<value>"
      Then it fails validation on "<field>"

      Examples:
        | field      | value        |
        | email      |              |
        | email      | not-an-email |
        | name       |              |
        | name       | 101 chars    |
        | role       | Manager      |
        | language   | fr           |
        | timeFormat | 36h          |

  Rule: Only admins create users

    Scenario Outline: Forbids non-admins
      Given <user> is signed in
      When <user> creates the employee "Fay" in team "Warehouse"
      Then it is forbidden

      Examples:
        | user |
        | Sam  |
        | Eve  |
