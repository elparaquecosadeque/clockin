using LinqToDB.Mapping;

namespace ClockIn.Persistence.Timesheets;

[Table("timesheet_history")]
public sealed class TimesheetHistoryRecord
{
    [Column("timesheet_id"), PrimaryKey(0)]
    public required Guid TimesheetId { get; init; }

    [Column("position"), PrimaryKey(1)]
    public required int Position { get; init; }

    [Column("kind")]
    public required string Kind { get; init; }

    [Column("actor_id")]
    public required Guid ActorId { get; init; }

    [Column("at")]
    public required DateTimeOffset At { get; init; }

    [Column("comment")]
    public string? Comment { get; init; }

    [Column("entry_id")]
    public Guid? EntryId { get; init; }

    [Column("before_start")]
    public DateTimeOffset? BeforeStart { get; init; }

    [Column("before_end")]
    public DateTimeOffset? BeforeEnd { get; init; }

    [Column("before_punched_start")]
    public DateTimeOffset? BeforePunchedStart { get; init; }

    [Column("before_punched_end")]
    public DateTimeOffset? BeforePunchedEnd { get; init; }

    [Column("after_start")]
    public DateTimeOffset? AfterStart { get; init; }

    [Column("after_end")]
    public DateTimeOffset? AfterEnd { get; init; }

    [Column("after_punched_start")]
    public DateTimeOffset? AfterPunchedStart { get; init; }

    [Column("after_punched_end")]
    public DateTimeOffset? AfterPunchedEnd { get; init; }
}
