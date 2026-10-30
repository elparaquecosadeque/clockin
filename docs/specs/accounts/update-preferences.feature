Feature: Update own preferences
  Language and time format follow the user across devices.

  Scenario: Saves language and time format
    Given Eve has language "en" and time format "24h"
    When Eve sets language "es" and time format "12h"
    Then /me returns language "es" and time format "12h"

  Scenario Outline: Rejects unsupported values
    When Eve sets <field> to "<value>"
    Then it fails validation on "<field>"

    Examples:
      | field      | value |
      | language   | pt    |
      | language   |       |
      | timeFormat | 24    |

  Scenario: Does not change the security stamp
    When Eve sets language "es"
    Then Eve's sessions remain valid
