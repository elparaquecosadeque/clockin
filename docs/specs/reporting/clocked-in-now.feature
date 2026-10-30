Feature: Who is clocked in now

  Background:
    Given it is Friday 2026-10-30 10:00
    And Eve clocked in at 08:00 and has not clocked out
    And Olga clocked in at 09:30 and has not clocked out
    And Sam clocked in at 07:00 and has not clocked out

  Scenario: The supervisor sees their team
    Given Sam is signed in
    When Sam views who is clocked in
    Then Sam sees Eve, since 08:00, for 2:00

  Scenario: Admins see everyone, supervisors included
    Given Ada is signed in
    When Ada views who is clocked in
    Then Ada sees Sam, Eve and Olga with their team and start time

  Scenario: Flags an entry open for more than 16 hours
    Given Ed clocked in yesterday at 08:00 and has not clocked out
    And Sam is signed in
    When Sam views who is clocked in
    Then Ed is listed and flagged as "over 16 hours"

  Scenario: Forbids employees
    Given Eve is signed in
    When Eve views who is clocked in
    Then it is forbidden
