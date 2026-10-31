namespace ClockIn.Domain.Accounts;

/// <summary>Language and time format follow the user across devices (ADR 0001).</summary>
public static class Preferences
{
    public const string English = "en";
    public const string Spanish = "es";
    public const string TwentyFourHour = "24h";
    public const string TwelveHour = "12h";

    public static readonly IReadOnlyList<string> Languages = [English, Spanish];
    public static readonly IReadOnlyList<string> TimeFormats = [TwentyFourHour, TwelveHour];
}
