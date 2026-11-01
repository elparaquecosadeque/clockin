using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Reporting.Queries;

/// <summary>
/// One row per entry of each Approved timesheet of the week. The team is the one the timesheet had when approved,
/// which is also what scopes a supervisor's export.
/// </summary>
public sealed record ExportApprovedCsvQuery(DateOnly WeekStart, Guid? TeamId);

public sealed class ExportApprovedCsvValidator : AbstractValidator<ExportApprovedCsvQuery>
{
    public ExportApprovedCsvValidator() =>
        RuleFor(q => q.WeekStart).Must(d => d.DayOfWeek == DayOfWeek.Monday).WithMessage("A week starts on a Monday.");
}

public sealed class ExportApprovedCsvHandler(
    ICurrentUser currentUser,
    IReportingReads reads,
    TimeProvider time)
{
    public async Task<CsvFileDto> HandleAsync(ExportApprovedCsvQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        TeamRow? filter = scope.FilterTeam(query.TeamId);
        if (!scope.IsAdmin && filter is null)
        {
            // A supervisor who leads no team has nothing to export.
            return File(query, []);
        }

        var people = scope.People.Where(p => p.Role.Can(Permissions.PunchTime)).ToDictionary(p => p.Id);
        IReadOnlyList<TimesheetRow> approved = [.. (await reads.TimesheetsAsync(people.Keys, query.WeekStart, ct))
            .Where(t => t.Status == TimesheetStatus.Approved && (filter is null || t.ApprovedTeamId == filter.Id))];

        return File(query, approved
            .SelectMany(t => t.Entries.Where(e => e.End is not null).Select(e => (Timesheet: t, Entry: e, Owner: people[t.OwnerId])))
            .OrderBy(x => x.Owner.Name, StringComparer.Ordinal).ThenBy(x => x.Entry.Start)
            .Select(x => new ApprovedHoursLine(
                x.Owner.Name, x.Owner.Email, scope.Team(x.Timesheet.ApprovedTeamId)?.Name, x.Timesheet.WeekStart, x.Entry.Start,
                x.Entry.End!.Value, x.Entry.IsManuallyChanged, x.Entry.Note)));
    }

    private CsvFileDto File(ExportApprovedCsvQuery query, IEnumerable<ApprovedHoursLine> lines) =>
        new($"clockin-approved-{query.WeekStart:yyyy-MM-dd}.csv", ApprovedHoursCsvHelper.Write(lines, time.LocalTimeZone));
}
