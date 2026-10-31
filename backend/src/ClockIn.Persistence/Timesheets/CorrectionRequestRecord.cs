using LinqToDB.Mapping;

namespace ClockIn.Persistence.Timesheets;

[Table("correction_requests")]
public sealed class CorrectionRequestRecord
{
    [Column("id"), PrimaryKey]
    public required Guid Id { get; init; }

    [Column("timesheet_id")]
    public required Guid TimesheetId { get; init; }

    [Column("owner_id")]
    public required Guid OwnerId { get; init; }

    [Column("reason")]
    public required string Reason { get; init; }

    [Column("requested_at")]
    public required DateTimeOffset RequestedAt { get; init; }

    [Column("status")]
    public required string Status { get; init; }

    [Column("decided_by")]
    public Guid? DecidedBy { get; init; }

    [Column("decided_at")]
    public DateTimeOffset? DecidedAt { get; init; }

    [Column("decision_comment")]
    public string? DecisionComment { get; init; }

    [Column("version")]
    public required int Version { get; init; }
}
