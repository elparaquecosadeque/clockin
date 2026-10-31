using LinqToDB.Mapping;

namespace ClockIn.Persistence.Seeds;

/// <summary>When the demo data was last rebuilt. At most one row.</summary>
[Table("demo_seeds")]
public sealed class DemoSeedRecord
{
    [Column("seeded_at")]
    public required DateTimeOffset SeededAt { get; init; }
}
