using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

[Migration(202611011200)]
public sealed class M202611011200_IndexReportingReads : Migration
{
    public override void Up()
    {
        Create.Index("ix_timesheets_owner_status_week").OnTable("timesheets")
            .OnColumn("owner_id").Ascending().OnColumn("status").Ascending().OnColumn("week_start").Ascending().OnColumn("id").Ascending();
        Create.Index("ix_corrections_owner_status_requested").OnTable("correction_requests")
            .OnColumn("owner_id").Ascending().OnColumn("status").Ascending().OnColumn("requested_at").Ascending().OnColumn("id").Ascending();
        Create.Index("ix_corrections_owner_status_decided").OnTable("correction_requests")
            .OnColumn("owner_id").Ascending().OnColumn("status").Ascending().OnColumn("decided_at").Ascending();
        Create.Index("ix_history_timesheet_kind_entry_at").OnTable("timesheet_history")
            .OnColumn("timesheet_id").Ascending().OnColumn("kind").Ascending().OnColumn("entry_id").Ascending().OnColumn("at").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_history_timesheet_kind_entry_at").OnTable("timesheet_history");
        Delete.Index("ix_corrections_owner_status_decided").OnTable("correction_requests");
        Delete.Index("ix_corrections_owner_status_requested").OnTable("correction_requests");
        Delete.Index("ix_timesheets_owner_status_week").OnTable("timesheets");
    }
}
