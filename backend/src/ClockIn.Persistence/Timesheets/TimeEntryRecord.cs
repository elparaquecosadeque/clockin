using LinqToDB.Mapping;

namespace ClockIn.Persistence.Timesheets;

[Table("time_entries")]
public sealed class TimeEntryRecord
{
    [Column("id"), PrimaryKey]
    public required Guid Id { get; init; }

    [Column("timesheet_id")]
    public required Guid TimesheetId { get; init; }

    /// <summary>Copied from the timesheet for the one-open-entry-per-owner index.</summary>
    [Column("owner_id")]
    public required Guid OwnerId { get; init; }

    [Column("started_at")]
    public required DateTimeOffset StartedAt { get; init; }

    [Column("ended_at")]
    public required DateTimeOffset? EndedAt { get; init; }

    [Column("clock_in_ip")]
    public required string ClockInIp { get; init; }

    [Column("clock_in_user_agent")]
    public required string ClockInUserAgent { get; init; }

    [Column("clock_out_ip")]
    public required string? ClockOutIp { get; init; }

    [Column("clock_out_user_agent")]
    public required string? ClockOutUserAgent { get; init; }

    [Column("is_manually_changed")]
    public required bool IsManuallyChanged { get; init; }
}
