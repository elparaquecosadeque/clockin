using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Queries;

/// <summary>A week with totals, flags and history. <see cref="OwnerId"/> null means the signed-in user's own week.</summary>
public sealed record GetTimesheetQuery(Guid? OwnerId, DateOnly WeekStart);

public sealed class GetTimesheetValidator : AbstractValidator<GetTimesheetQuery>
{
    public GetTimesheetValidator() =>
        RuleFor(q => q.WeekStart).Must(d => d.DayOfWeek == DayOfWeek.Monday).WithMessage("A week starts on a Monday.");
}

public sealed class GetTimesheetHandler(
    ICurrentUser currentUser,
    TimesheetAccess access,
    IUserRepository usersRepository,
    ITimesheetRepository timesheetsRepository,
    ICorrectionRequestRepository correctionsRepository,
    TimeProvider time)
{
    // ponytail: reads through the aggregate, which already holds everything this page shows.
    public async Task<TimesheetDto> HandleAsync(GetTimesheetQuery query, CancellationToken ct)
    {
        UserId ownerId = query.OwnerId is { } id ? new UserId(id) : currentUser.Id;
        TimesheetParties parties = await access.PartiesAsync(ownerId, ct);
        TimesheetParties.Require(parties.CanView);

        var week = new Week(query.WeekStart);
        Timesheet? timesheet = await timesheetsRepository.FindAsync(ownerId, week, ct);
        CorrectionRequest? correction = timesheet is null ? null : await correctionsRepository.FindLatestAsync(timesheet.Id, ct);
        IReadOnlyDictionary<UserId, string> names = await usersRepository.NamesAsync(PeopleIn(timesheet, correction), ct);

        DateTimeOffset now = time.GetUtcNow();
        TimeZoneInfo zone = time.LocalTimeZone;
        IReadOnlyList<TimeEntry> entries = timesheet?.Entries ?? [];
        WeekStatus status = timesheet is null ? WeekStatus.NotStarted : Enum.Parse<WeekStatus>(timesheet.Status.ToString());
        List<TimesheetEntryDto> entryDtos = [.. entries.OrderBy(e => e.Start).Select(e => ToDto(e, now, zone, names))];

        return new TimesheetDto(
            timesheet?.Id.Value,
            timesheet?.Version ?? 0,
            ownerId.Value,
            parties.Owner.Name,
            parties.OwnerTeam?.Name,
            week.Start,
            status,
            timesheet?.ReturnComment,
            timesheet?.Approval is { } approval ? new ApprovalDto(Name(names, approval.ApproverId), approval.At) : null,
            entryDtos.Sum(e => e.Seconds),
            [.. Enumerable.Range(0, 7).Select(i => week.Start.AddDays(i)).Select(day => new DayTotalDto(
                day,
                entryDtos.Where(e => e.Date == day).Sum(e => e.Seconds),
                entryDtos.Any(e => e.Date == day && e.End is null)))],
            entryDtos,
            [.. (timesheet?.History ?? []).Select(h => ToDto(h, ownerId, names))],
            correction is null ? null : ToDto(correction, names),
            Actions(parties, status, correction is { Status: CorrectionStatus.Pending }, now >= week.EndsAt(zone)),
            zone.Id);
    }

    private static TimesheetActionsDto Actions(TimesheetParties parties, WeekStatus status, bool correctionPending, bool weekEnded)
    {
        bool editable = status is WeekStatus.NotStarted or WeekStatus.Draft;
        bool locked = status is WeekStatus.Submitted or WeekStatus.Approved;
        return new TimesheetActionsDto(
            ChangeEntries: parties.CanChange && editable,
            Submit: parties.CanChange && editable && weekEnded,
            Approve: parties.IsApprover && status == WeekStatus.Submitted && !correctionPending,
            Return: parties.IsApprover && status == WeekStatus.Submitted && !correctionPending,
            Reopen: parties.CanReopen && status == WeekStatus.Approved && !correctionPending,
            RequestCorrection: parties.IsOwner && parties.CanChange && locked && !correctionPending,
            DecideCorrection: parties.IsApprover && correctionPending);
    }

    private static IEnumerable<UserId> PeopleIn(Timesheet? timesheet, CorrectionRequest? correction)
    {
        if (timesheet is null)
        {
            return [];
        }

        return timesheet.History.Select(h => h.ActorId)
            .Concat(timesheet.Entries.Select(e => e.ChangedBy).OfType<UserId>())
            .Concat(timesheet.Approval is { } a ? [a.ApproverId] : [])
            .Concat(correction?.DecidedBy is { } d ? [d] : [])
            .Distinct();
    }

    private static string Name(IReadOnlyDictionary<UserId, string> names, UserId id) => names.GetValueOrDefault(id, "?");

    private static TimesheetEntryDto ToDto(TimeEntry entry, DateTimeOffset now, TimeZoneInfo zone, IReadOnlyDictionary<UserId, string> names) =>
        new(
            entry.Id.Value,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.Start, zone).DateTime),
            entry.Start,
            entry.End,
            (long)((entry.End ?? now) - entry.Start).TotalSeconds,
            entry.ManualChange,
            entry.ChangedBy is { } by ? Name(names, by) : null,
            entry.Note,
            entry.PunchedStart,
            entry.PunchedEnd);

    private static HistoryItemDto ToDto(TimesheetEvent item, UserId ownerId, IReadOnlyDictionary<UserId, string> names) =>
        new(item.Kind, Name(names, item.ActorId), item.ActorId == ownerId, item.At, item.Comment, ToDto(item.Before), ToDto(item.After));

    private static EntryTimesDto? ToDto(EntrySnapshot? snapshot) =>
        snapshot is null ? null : new(snapshot.EntryId.Value, snapshot.Start, snapshot.End, snapshot.PunchedStart, snapshot.PunchedEnd);

    private static CorrectionRequestDto ToDto(CorrectionRequest request, IReadOnlyDictionary<UserId, string> names) =>
        new(
            request.Id.Value, request.Status, request.Reason, request.RequestedAt,
            request.DecidedBy is { } by ? Name(names, by) : null, request.DecidedAt, request.DecisionComment);
}
