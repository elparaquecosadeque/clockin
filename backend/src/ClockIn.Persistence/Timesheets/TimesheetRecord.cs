using LinqToDB.Mapping;

namespace ClockIn.Persistence.Timesheets;

[Table("timesheets")]
public sealed class TimesheetRecord
{
    [Column("id"), PrimaryKey]
    public required Guid Id { get; init; }

    [Column("owner_id")]
    public required Guid OwnerId { get; init; }

    [Column("week_start")]
    public required DateOnly WeekStart { get; init; }

    [Column("status")]
    public required string Status { get; init; }

    [Column("version")]
    public required int Version { get; init; }

    [Column("approved_by")]
    public Guid? ApprovedBy { get; init; }

    [Column("approved_at")]
    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>The owner's team when approved; exports keep it after moves.</summary>
    [Column("approved_team_id")]
    public Guid? ApprovedTeamId { get; init; }

    [Column("return_comment")]
    public string? ReturnComment { get; init; }
}
