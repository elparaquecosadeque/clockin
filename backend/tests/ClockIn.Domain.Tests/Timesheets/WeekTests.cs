using AwesomeAssertions;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Timesheets;

/// <summary>clock-in.feature, rule: an entry belongs to the week, in company time, in which it started.</summary>
public sealed class WeekTests
{
    [Theory]
    [InlineData("2026-11-01T23:59:59-05:00", "2026-10-26")]
    [InlineData("2026-11-02T00:00:00-05:00", "2026-11-02")]
    [InlineData("2026-11-02T04:59:59Z", "2026-10-26")]
    [InlineData("2026-11-02T05:00:00Z", "2026-11-02")]
    [InlineData("2026-10-28T12:00:00-05:00", "2026-10-26")]
    public void Starts_on_the_company_monday_when_given_any_instant(string instant, string monday)
    {
        var week = Week.Containing(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture), CompanyClock.Zone);

        week.Start.Should().Be(DateOnly.Parse(monday, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("2026-10-26 00:00", true)]
    [InlineData("2026-11-01 23:59:59", true)]
    [InlineData("2026-10-25 23:59:59", false)]
    [InlineData("2026-11-02 00:00", false)]
    public void Contains_only_instants_from_monday_to_sunday_in_company_time(string companyTime, bool contained)
    {
        var week = Week.Containing(CompanyClock.At("2026-10-28 12:00"), CompanyClock.Zone);

        week.Contains(CompanyClock.At(companyTime), CompanyClock.Zone).Should().Be(contained);
    }
}
