using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

[Migration(202610302300)]
public sealed class M202610302300_AddPreferencesToUsers : Migration
{
    public override void Up()
    {
        Alter.Table("users")
            .AddColumn("language").AsString(2).NotNullable().WithDefaultValue("en")
            .AddColumn("time_format").AsString(3).NotNullable().WithDefaultValue("24h");
    }

    public override void Down()
    {
        Delete.Column("time_format").FromTable("users");
        Delete.Column("language").FromTable("users");
    }
}
