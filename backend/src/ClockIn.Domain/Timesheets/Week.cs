namespace ClockIn.Domain.Timesheets;

/// <summary>Monday 00:00 to Sunday 23:59:59 in company time, named by its Monday.</summary>
public sealed record Week
{
    public Week(DateOnly start)
    {
        if (start.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException($"A week starts on a Monday; {start} is a {start.DayOfWeek}.", nameof(start));
        }

        Start = start;
    }

    public DateOnly Start { get; }

    public static Week Containing(DateTimeOffset instant, TimeZoneInfo companyZone)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, companyZone).DateTime);
        int daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;
        return new Week(day.AddDays(-daysSinceMonday));
    }

    /// <summary>The next Monday 00:00 in company time.</summary>
    public DateTimeOffset EndsAt(TimeZoneInfo companyZone)
    {
        var local = Start.AddDays(7).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, companyZone.GetUtcOffset(local));
    }

    public bool Contains(DateTimeOffset instant, TimeZoneInfo companyZone) => Containing(instant, companyZone) == this;
}
