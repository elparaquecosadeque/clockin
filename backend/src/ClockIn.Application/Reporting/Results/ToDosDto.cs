namespace ClockIn.Application.Reporting.Results;

/// <summary>
/// Counts derived from stored state on each load (to-do-badges.feature). The week lists let the page link to them.
/// Review counts are zero for people who review nobody.
/// </summary>
public sealed record ToDosDto(
    DateTimeOffset? ClockedInSince,
    IReadOnlyList<DateOnly> ReturnedWeeks,
    IReadOnlyList<DateOnly> UnsubmittedWeeks,
    int DeclinedCorrections,
    int TimesheetsToReview,
    int CorrectionsToDecide);
