using LinqToDB.Mapping;

namespace ClockIn.Persistence.Teams;

[Table("teams")]
public sealed class TeamRecord
{
    [Column("id"), PrimaryKey]
    public required Guid Id { get; init; }

    [Column("name")]
    public required string Name { get; init; }

    [Column("normalized_name")]
    public required string NormalizedName { get; init; }

    [Column("supervisor_id")]
    public required Guid SupervisorId { get; init; }

    [Column("is_archived")]
    public required bool IsArchived { get; init; }

    [Column("version")]
    public required int Version { get; init; }
}
