Feature: See own team and supervisor
  Employees know which team they are in and who reviews their timesheets.

  Scenario: An employee sees their team and supervisor in the account menu
    Given Eve is an employee in the Warehouse team, supervised by Sam
    When Eve opens the account menu
    Then it shows "Warehouse · Supervisor: Sam"

  Scenario: The supervisor shown follows a supervisor change
    Given Eve is in the Warehouse team, supervised by Sam
    When an admin makes Sid the Warehouse supervisor
    And Eve signs in again
    Then the account menu shows "Warehouse · Supervisor: Sid"

  Scenario: Users without a team see no team line
    Given Ada is an admin, without a team
    When Ada opens the account menu
    Then it shows no team or supervisor
