using System.Globalization;
using System.Text;

namespace ClockIn.Application.Reporting;

/// <summary>
/// The payroll file (export-approved-csv.feature): UTF-8 with a byte order mark, commas, CRLF, English headers and
/// invariant numbers whatever the user's language.
/// </summary>
public static class ApprovedHoursCsv
{
    public const string Header = "Employee,Email,Team,WeekStart,Date,ClockIn,ClockOut,Hours,HoursDecimal,ManuallyChanged,Note";

    public static byte[] Write(IEnumerable<ApprovedHoursLine> lines, TimeZoneInfo zone)
    {
        var csv = new StringBuilder(Header).Append("\r\n");
        foreach (ApprovedHoursLine line in lines)
        {
            DateTimeOffset start = TimeZoneInfo.ConvertTime(line.Start, zone);
            DateTimeOffset end = TimeZoneInfo.ConvertTime(line.End, zone);
            double seconds = (line.End - line.Start).TotalSeconds;
            string[] cells =
            [
                Cell(line.Employee),
                Cell(line.Email),
                Cell(line.Team ?? ""),
                line.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                start.ToString("HH:mm", CultureInfo.InvariantCulture),
                end.ToString("HH:mm", CultureInfo.InvariantCulture),
                Hours(seconds),
                Math.Round(seconds / 3600, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture),
                line.ManuallyChanged ? "true" : "false",
                Cell(line.Note ?? ""),
            ];
            csv.AppendJoin(',', cells).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    /// <summary>Whole minutes, rounded from the exact seconds: 7:44:30 is 7:45.</summary>
    private static string Hours(double seconds)
    {
        long minutes = (long)Math.Round(seconds / 60, MidpointRounding.AwayFromZero);
        return $"{minutes / 60}:{minutes % 60:00}";
    }

    /// <summary>Neutralises spreadsheet formulas, then quotes when the text needs it.</summary>
    public static string Cell(string text)
    {
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;
    }
}
