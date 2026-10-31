using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

[Migration(202610302200)]
public sealed class M202610302200_AddTimesheetWorkflow : Migration
{
    private const string Timestamp = "timestamptz";

    public override void Up()
    {
        Alter.Table("timesheets")
            .AddColumn("approved_by").AsGuid().Nullable()
            .AddColumn("approved_at").AsCustom(Timestamp).Nullable()
            .AddColumn("approved_team_id").AsGuid().Nullable()
            .AddColumn("return_comment").AsString(1000).Nullable();

        Alter.Table("time_entries")
            .AddColumn("manual_change").AsString(16).NotNullable().WithDefaultValue("None")
            .AddColumn("changed_by").AsGuid().Nullable()
            .AddColumn("note").AsString(500).Nullable()
            .AddColumn("punched_start").AsCustom(Timestamp).Nullable()
            .AddColumn("punched_end").AsCustom(Timestamp).Nullable();

        // Append-only; position orders rows within a timesheet and makes concurrent appends collide.
        // The composite key is declared inline: SQLite can't add a primary key to an existing table.
        Create.Table("timesheet_history")
            .WithColumn("timesheet_id").AsGuid().NotNullable().PrimaryKey("pk_timesheet_history")
                .ForeignKey("fk_timesheet_history_timesheets", "timesheets", "id")
            .WithColumn("position").AsInt32().NotNullable().PrimaryKey("pk_timesheet_history")
            .WithColumn("kind").AsString(32).NotNullable()
            .WithColumn("actor_id").AsGuid().NotNullable()
            .WithColumn("at").AsCustom(Timestamp).NotNullable()
            .WithColumn("comment").AsString(1000).Nullable()
            .WithColumn("entry_id").AsGuid().Nullable()
            .WithColumn("before_start").AsCustom(Timestamp).Nullable()
            .WithColumn("before_end").AsCustom(Timestamp).Nullable()
            .WithColumn("before_punched_start").AsCustom(Timestamp).Nullable()
            .WithColumn("before_punched_end").AsCustom(Timestamp).Nullable()
            .WithColumn("after_start").AsCustom(Timestamp).Nullable()
            .WithColumn("after_end").AsCustom(Timestamp).Nullable()
            .WithColumn("after_punched_start").AsCustom(Timestamp).Nullable()
            .WithColumn("after_punched_end").AsCustom(Timestamp).Nullable();

        Create.Table("correction_requests")
            .WithColumn("id").AsGuid().PrimaryKey("pk_correction_requests")
            .WithColumn("timesheet_id").AsGuid().NotNullable().ForeignKey("fk_correction_requests_timesheets", "timesheets", "id")
            .WithColumn("owner_id").AsGuid().NotNullable()
            .WithColumn("reason").AsString(1000).NotNullable()
            .WithColumn("requested_at").AsCustom(Timestamp).NotNullable()
            .WithColumn("status").AsString(16).NotNullable()
            .WithColumn("decided_by").AsGuid().Nullable()
            .WithColumn("decided_at").AsCustom(Timestamp).Nullable()
            .WithColumn("decision_comment").AsString(1000).Nullable()
            .WithColumn("version").AsInt32().NotNullable();

        Create.Index("ix_correction_requests_timesheet").OnTable("correction_requests").OnColumn("timesheet_id");

        // At most one pending request per timesheet, even under concurrency (request-correction.feature).
        Execute.Sql("CREATE UNIQUE INDEX ux_correction_requests_one_pending ON correction_requests (timesheet_id) WHERE status = 'Pending'");
    }

    public override void Down()
    {
        Delete.Table("correction_requests");
        Delete.Table("timesheet_history");
        Delete.Column("approved_by").Column("approved_at").Column("approved_team_id").Column("return_comment").FromTable("timesheets");
        Delete.Column("manual_change").Column("changed_by").Column("note").Column("punched_start").Column("punched_end").FromTable("time_entries");
    }
}
