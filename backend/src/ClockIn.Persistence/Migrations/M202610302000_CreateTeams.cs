using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

[Migration(202610302000)]
public sealed class M202610302000_CreateTeams : Migration
{
    public override void Up()
    {
        Create.Table("teams")
            .WithColumn("id").AsGuid().PrimaryKey("pk_teams")
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("normalized_name").AsString(100).NotNullable()
            .WithColumn("supervisor_id").AsGuid().NotNullable().ForeignKey("fk_teams_users", "users", "id")
            .WithColumn("is_archived").AsBoolean().NotNullable()
            .WithColumn("version").AsInt32().NotNullable();

        // Names are unique ignoring case and spaces, archived teams included (create-team.feature).
        Create.Index("ux_teams_normalized_name").OnTable("teams").OnColumn("normalized_name").Unique();

        // A supervisor leads at most one active team, even under concurrency.
        Execute.Sql("CREATE UNIQUE INDEX ux_teams_one_active_per_supervisor ON teams (supervisor_id) WHERE is_archived = FALSE");

        Alter.Table("users")
            .AddColumn("team_id").AsGuid().Nullable().ForeignKey("fk_users_teams", "teams", "id");
    }

    public override void Down()
    {
        Delete.ForeignKey("fk_users_teams").OnTable("users");
        Delete.Column("team_id").FromTable("users");
        Delete.Table("teams");
    }
}
