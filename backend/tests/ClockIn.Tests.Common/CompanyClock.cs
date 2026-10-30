using System.Globalization;

namespace ClockIn.Tests.Common;

/// <summary>
/// A settable clock in the company timezone (<c>America/Bogota</c>, UTC−5, no DST), as the specs assume.
/// Times are written in company time, like the specs: <c>"2026-10-30 10:00"</c>.
/// </summary>
public sealed class CompanyClock : TimeProvider
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private DateTimeOffset _now;

    public CompanyClock(string now = "2026-10-30 10:00") => _now = At(now);

    public override TimeZoneInfo LocalTimeZone => Zone;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Set(string now) => _now = At(now);

    /// <summary>Company-local time to a UTC instant. Accepts <c>yyyy-MM-dd HH:mm</c> or <c>yyyy-MM-dd HH:mm:ss</c>.</summary>
    public static DateTimeOffset At(string companyTime)
    {
        var local = DateTime.ParseExact(companyTime, ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"], CultureInfo.InvariantCulture);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
