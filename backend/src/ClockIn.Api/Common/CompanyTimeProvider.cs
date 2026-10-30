namespace ClockIn.Api.Common;

/// <summary>The system clock, with the company timezone as its local zone, which the domain uses for weeks.</summary>
public sealed class CompanyTimeProvider(TimeZoneInfo companyZone) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => companyZone;
}
