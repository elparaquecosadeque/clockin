using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Demo.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ClockIn.PublicApi.Common;

/// <summary>Read-only demo examples; reload Scalar after a write or demo reset to refresh IDs and versions.</summary>
public sealed partial class MiaExamplesTransformer(
    IDemoData demo,
    IUserRepository usersRepository,
    ITimesheetRepository timesheetsRepository,
    TimeProvider time,
    ILogger<MiaExamplesTransformer> logger) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Description = "Examples assume Mia: sign in as mia@clockin.demo, then set Bearer authentication. "
            + "Punch in and out use server time and need no body. Read, edit and submit use Mia's previous demo week. "
            + "Reload Scalar after changes or a demo reset to refresh the entry ID and version.";
        try
        {
            DateTimeOffset seeded = await demo.LastSeededAtAsync(cancellationToken) ?? time.GetUtcNow();
            var week = new Week(Week.Containing(seeded, time.LocalTimeZone).Start.AddDays(-7));
            string weekStart = week.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (string route in new[] { "/api/v1/timesheets/mine/{weekStart}", "/api/v1/timesheets/mine/{weekStart}/submit" })
            {
                OpenApiOperation operation = document.Paths[route].Operations!.Values.Single();
                foreach (OpenApiParameter parameter in operation.Parameters!.OfType<OpenApiParameter>())
                {
                    if (parameter.Name == "weekStart")
                    {
                        parameter.Example = JsonValue.Create(weekStart);
                    }
                }
            }

            User? mia = await usersRepository.FindByEmailAsync(new Email(demo.Accounts.Single(a => a.Name == "Mia").Email), cancellationToken);
            Timesheet? sheet = mia is null ? null : await timesheetsRepository.FindAsync(mia.Id, week, cancellationToken);
            TimeEntry? entry = sheet?.Entries.OrderBy(e => e.Start).FirstOrDefault();
            if (sheet is null || entry is null)
            {
                document.Info.Description += " No entry is available for Mia in that week; prepare the demo data first.";
                return;
            }

            OpenApiOperation edit = document.Paths["/api/v1/time-entries/{entryId}"].Operations!.Values.Single();
            foreach (OpenApiParameter parameter in edit.Parameters!.OfType<OpenApiParameter>())
            {
                if (parameter.Name == "entryId")
                {
                    parameter.Example = JsonValue.Create(entry.Id.Value.ToString());
                }
            }

            foreach (OpenApiMediaType media in edit.RequestBody!.Content!.Values)
            {
                media.Examples = new Dictionary<string, IOpenApiExample>
                {
                    ["Mia"] = new OpenApiExample
                    {
                        Summary = "Mia's entry (read current version before repeating)",
                        Value = JsonSerializer.SerializeToNode(new
                        {
                            start = entry.Start,
                            end = entry.End,
                            note = "Reviewed and confirmed through Scalar",
                            version = sheet.Version,
                        }),
                    },
                };
            }

            if (sheet.Status != TimesheetStatus.Draft)
            {
                document.Info.Description += " Mia's example week is currently locked; editing or submitting it will be rejected.";
            }
        }
        catch (DbException)
        {
            LogExamplesUnavailable(logger);
            document.Info.Description += " Prepare the shared demo database to load Mia's entry examples.";
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mia's Scalar examples are unavailable; prepare the shared demo database first.")]
    private static partial void LogExamplesUnavailable(ILogger logger);
}
