Feature: Review queue
  Submitted weeks and pending correction requests in the approver's existing scope.
  Each list is paged independently in SQL, with 50 items per page and a has-more flag.
  Submitted weeks sort by week, latest submission time and id; requests by requested time and id.

  @api
  Scenario: Pages submitted weeks without advancing correction requests
    Given the approver has 52 submitted weeks and 52 pending correction requests
    When the approver requests timesheet page 2 and correction page 1
    Then the queue contains the remaining 2 submitted weeks and the first 50 requests
    And no other team's work is included
    And neither submitted weeks nor requests repeat across unchanged pages

  @api
  Scenario: Rejects invalid page numbers
    When an approver requests a nonpositive page or a page whose offset would overflow
    Then the response is 400 Bad Request
