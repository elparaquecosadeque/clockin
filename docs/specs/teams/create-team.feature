Feature: Create a team
  A team always has exactly one supervisor. A supervisor leads at most one team.

  Background:
    Given Ada is signed in
    And "Sid" is an active Supervisor who leads no team

  Scenario: Creates a team led by a free supervisor
    When Ada creates team "Night Shift" led by Sid
    Then team "Night Shift" exists, active, with supervisor Sid and no employees

  Scenario: Accepts a supervisor who has not activated yet
    Given "Pat" is a PendingActivation Supervisor who leads no team
    When Ada creates team "Night Shift" led by Pat
    Then team "Night Shift" exists with supervisor Pat

  Rule: Team names are unique, including archived teams

    Scenario Outline: Rejects a name already in use
      Given team "Old Store" is archived
      When Ada creates team "<name>" led by Sid
      Then it conflicts with "TeamNameInUse"

      Examples:
        | name           |
        | Warehouse      |
        | warehouse      |
        | "  Warehouse " |
        | Old Store      |

    Scenario Outline: Rejects an invalid name
      When Ada creates team "<name>" led by Sid
      Then it fails validation on "name"

      Examples:
        | name      |
        | (missing) |
        |           |
        | "   "     |
        | 101 chars |

  Rule: The supervisor must be a free, non-deactivated supervisor

    Scenario: Rejects a supervisor who already leads a team
      When Ada creates team "Night Shift" led by Sam
      Then it breaks the rule "SupervisorAlreadyLeadsTeam"

    Scenario Outline: Rejects a user who cannot lead
      When Ada creates team "Night Shift" led by <user>
      Then it breaks the rule "<code>"

      Examples:
        | user                | code                |
        | Eve                 | UserIsNotSupervisor |
        | Abe                 | UserIsNotSupervisor |
        | a deactivated "Rob" | UserIsDeactivated   |

    Scenario: Rejects a team without a supervisor
      When Ada creates team "Night Shift" without a supervisor
      Then it fails validation on "supervisorId"

  Scenario Outline: Forbids non-admins
    Given <user> is signed in
    When <user> creates team "Night Shift" led by Sid
    Then it is forbidden

    Examples:
      | user |
      | Sam  |
      | Eve  |
