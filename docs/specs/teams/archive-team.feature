Feature: Archive a team
  Teams are never deleted. An archived team keeps its name in history and exports.

  Background:
    Given Ada is signed in
    And team "Night Shift" is led by "Sid" and its only employee "Rob" is deactivated

  Scenario: Archives a team with no active employees
    When Ada archives team "Night Shift"
    Then team "Night Shift" is archived
    And Sid leads no team and can be given another one
    And Rob's timesheets still show team "Night Shift"

  Scenario: Archives a team with no employees at all
    Given team "Empty" is led by "Pat" and has no employees
    When Ada archives team "Empty"
    Then team "Empty" is archived

  Rule: Nothing in the team still needs a decision

    Scenario: Rejects a team with active employees
      When Ada archives team "Warehouse"
      Then it breaks the rule "TeamHasActiveEmployees"

    Scenario: Rejects a team with a pending employee
      Given "Fay" is PendingActivation in team "Night Shift"
      When Ada archives team "Night Shift"
      Then it breaks the rule "TeamHasActiveEmployees"

    Scenario: Rejects a team with Submitted timesheets
      Given Rob has a Submitted timesheet for week 2026-10-19
      When Ada archives team "Night Shift"
      Then it breaks the rule "TeamHasPendingReviews"

    Scenario: Rejects a team with pending correction requests
      Given Rob has a pending correction request
      When Ada archives team "Night Shift"
      Then it breaks the rule "TeamHasPendingReviews"

  Scenario: Rejects a team that is already archived
    Given team "Old Store" is archived
    When Ada archives team "Old Store"
    Then it breaks the rule "TeamIsArchived"

  Scenario: Forbids non-admins
    Given Sid is signed in
    When Sid archives team "Night Shift"
    Then it is forbidden
