using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

/// <summary>Stays empty outside the Development and Demo environments (ADR 0005).</summary>
[Migration(202610302100)]
public sealed class M202610302100_CreateDemoSeeds : Migration
{
    // See M202610301700_CreateTimesheets.
    private const string Timestamp = "timestamptz";

    public override void Up() =>
        Create.Table("demo_seeds").WithColumn("seeded_at").AsCustom(Timestamp).NotNullable();

    public override void Down() => Delete.Table("demo_seeds");
}
