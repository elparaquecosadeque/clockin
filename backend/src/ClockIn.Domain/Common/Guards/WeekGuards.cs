using Ardalis.GuardClauses;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Domain.Common.Guards;

public static class WeekGuards
{
    public static DateTimeOffset OutsideWeek(this IGuardClause guard, DateTimeOffset instant, Week week, TimeZoneInfo companyZone)
    {
        if (!week.Contains(instant, companyZone))
        {
            throw new ArgumentException($"{instant:O} is outside the week of {week.Start}.", nameof(instant));
        }

        return instant;
    }
}
