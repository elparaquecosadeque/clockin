using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Persistence.Common;
using LinqToDB;
using LinqToDB.Async;

namespace ClockIn.Persistence.Timesheets;

public sealed class CorrectionRequestRepository(AppDataConnection db, PendingDomainEvents events) : ICorrectionRequestRepository
{
    private const string Pending = nameof(CorrectionStatus.Pending);

    public async Task<CorrectionRequest?> FindAsync(CorrectionRequestId id, CancellationToken ct) =>
        ToDomain(await db.GetTable<CorrectionRequestRecord>().FirstOrDefaultAsync(r => r.Id == id.Value, ct));

    public async Task<CorrectionRequest?> FindPendingAsync(TimesheetId timesheetId, CancellationToken ct) =>
        ToDomain(await db.GetTable<CorrectionRequestRecord>()
            .FirstOrDefaultAsync(r => r.TimesheetId == timesheetId.Value && r.Status == Pending, ct));

    public async Task<CorrectionRequest?> FindLatestAsync(TimesheetId timesheetId, CancellationToken ct) =>
        ToDomain(await db.GetTable<CorrectionRequestRecord>()
            .Where(r => r.TimesheetId == timesheetId.Value)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(ct));

    public async Task SaveAsync(CorrectionRequest request, CancellationToken ct)
    {
        if (request.Version == 0)
        {
            try
            {
                await db.InsertAsync(ToRecord(request), token: ct);
            }
            catch (Exception ex) when (UniqueViolation.Is(ex))
            {
                throw new ConflictException("CorrectionRequestAlreadyPending", "A correction request on this week is already waiting.");
            }
        }
        else
        {
            int updated = await db.GetTable<CorrectionRequestRecord>()
                .Where(r => r.Id == request.Id.Value && r.Version == request.Version)
                .Set(r => r.Status, request.Status.ToString())
                .Set(r => r.DecidedBy, request.DecidedBy == null ? null : request.DecidedBy.Value.Value)
                .Set(r => r.DecidedAt, request.DecidedAt)
                .Set(r => r.DecisionComment, request.DecisionComment)
                .Set(r => r.Version, request.Version + 1)
                .UpdateAsync(ct);
            if (updated == 0)
            {
                throw new ConflictException("ConcurrencyConflict", "The request was changed by someone else. Reload and try again.");
            }
        }

        events.CollectFrom(request);
    }

    private static CorrectionRequest? ToDomain(CorrectionRequestRecord? record) =>
        record is null
            ? null
            : CorrectionRequest.Restore(
                new CorrectionRequestId(record.Id),
                new TimesheetId(record.TimesheetId),
                new UserId(record.OwnerId),
                record.Reason,
                record.RequestedAt,
                Enum.Parse<CorrectionStatus>(record.Status),
                record.DecidedBy is { } by ? new UserId(by) : null,
                record.DecidedAt,
                record.DecisionComment,
                record.Version);

    private static CorrectionRequestRecord ToRecord(CorrectionRequest request) => new()
    {
        Id = request.Id.Value,
        TimesheetId = request.TimesheetId.Value,
        OwnerId = request.OwnerId.Value,
        Reason = request.Reason,
        RequestedAt = request.RequestedAt,
        Status = request.Status.ToString(),
        DecidedBy = request.DecidedBy?.Value,
        DecidedAt = request.DecidedAt,
        DecisionComment = request.DecisionComment,
        Version = 1,
    };
}
