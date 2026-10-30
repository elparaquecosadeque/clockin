using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

[Migration(202610301700)]
public sealed class M202610301700_CreateTimesheets : Migration
{
    // FluentMigrator can't map DateTimeOffset on SQLite. PostgreSQL knows this type; SQLite accepts any name and keeps the text the driver writes.
    private const string Timestamp = "timestamptz";

    public override void Up()
    {
        Create.Table("timesheets")
            .WithColumn("id").AsGuid().PrimaryKey("pk_timesheets")
            .WithColumn("owner_id").AsGuid().NotNullable()
            .WithColumn("week_start").AsDate().NotNullable()
            .WithColumn("status").AsString(16).NotNullable()
            .WithColumn("version").AsInt32().NotNullable();

        Create.Index("ux_timesheets_owner_week").OnTable("timesheets")
            .OnColumn("owner_id").Ascending()
            .OnColumn("week_start").Ascending()
            .WithOptions().Unique();

        Create.Table("time_entries")
            .WithColumn("id").AsGuid().PrimaryKey("pk_time_entries")
            .WithColumn("timesheet_id").AsGuid().NotNullable().ForeignKey("fk_time_entries_timesheets", "timesheets", "id")
            .WithColumn("owner_id").AsGuid().NotNullable()
            .WithColumn("started_at").AsCustom(Timestamp).NotNullable()
            .WithColumn("ended_at").AsCustom(Timestamp).Nullable()
            .WithColumn("clock_in_ip").AsString(45).NotNullable()
            .WithColumn("clock_in_user_agent").AsString(512).NotNullable()
            .WithColumn("clock_out_ip").AsString(45).Nullable()
            .WithColumn("clock_out_user_agent").AsString(512).Nullable()
            .WithColumn("is_manually_changed").AsBoolean().NotNullable();

        Create.Index("ix_time_entries_timesheet").OnTable("time_entries").OnColumn("timesheet_id");

        // At most one open entry per person, even for two punches at the same moment (clock-in.feature).
        // owner_id is copied from the timesheet so this can be a plain partial index, which both engines support.
        Execute.Sql("CREATE UNIQUE INDEX ux_time_entries_one_open_per_owner ON time_entries (owner_id) WHERE ended_at IS NULL");
    }

    public override void Down()
    {
        Delete.Table("time_entries");
        Delete.Table("timesheets");
    }
}
