@e2e
Feature: Clock screen
  The main page for employees and supervisors. Mobile-first.

  Scenario: Shows a single big action that matches the state
    Given Eve is signed in on a 375px wide phone
    When Eve opens the clock screen
    Then it shows one button, "Clock in", with no horizontal scrolling
    When Eve taps it
    Then the button becomes "Clock out" and shows "clocked in since 08:00"

  Scenario: Shows today's and this week's running totals
    Given Eve clocked in at 08:00 and it is 10:00
    When Eve opens the clock screen
    Then it shows today 2:00 and the week total including today

  Scenario: Explains a too-long entry and links to the fix
    Given Eve clocked in yesterday at 08:00 and forgot to clock out
    When Eve taps "Clock out"
    Then a message explains that the entry is over 16 hours
    And it links to editing the entry's end time

  Scenario: Shows times in the user's format
    Given Eve's time format is "12h"
    When Eve opens the clock screen
    Then times look like "8:00 AM"

  Scenario: Works with the keyboard and passes axe
    When the clock screen is checked with axe and used with the keyboard only
    Then there are no violations and every action is reachable
