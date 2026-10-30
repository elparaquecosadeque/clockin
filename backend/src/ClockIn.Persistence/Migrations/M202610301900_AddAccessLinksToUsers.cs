using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

[Migration(202610301900)]
public sealed class M202610301900_AddAccessLinksToUsers : Migration
{
    // See M202610301700_CreateTimesheets.
    private const string Timestamp = "timestamptz";

    public override void Up()
    {
        Alter.Table("users")
            .AddColumn("access_link_hash").AsString(64).Nullable()
            .AddColumn("access_link_expires_at").AsCustom(Timestamp).Nullable();
    }

    public override void Down()
    {
        Delete.Column("access_link_expires_at").FromTable("users");
        Delete.Column("access_link_hash").FromTable("users");
    }
}
