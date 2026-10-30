using FluentMigrator;

namespace ClockIn.Persistence.Migrations;

[Migration(202610301800)]
public sealed class M202610301800_CreateUsersAndSessions : Migration
{
    // See M202610301700_CreateTimesheets.
    private const string Timestamp = "timestamptz";

    public override void Up()
    {
        Create.Table("users")
            .WithColumn("id").AsGuid().PrimaryKey("pk_users")
            .WithColumn("email").AsString(254).NotNullable()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("role").AsString(16).NotNullable()
            .WithColumn("status").AsString(24).NotNullable()
            .WithColumn("password_hash").AsString(256).Nullable()
            .WithColumn("security_stamp").AsGuid().NotNullable()
            .WithColumn("failed_sign_ins").AsInt32().NotNullable()
            .WithColumn("locked_until").AsCustom(Timestamp).Nullable()
            .WithColumn("version").AsInt32().NotNullable();

        // The domain stores emails trimmed and lowercased, so a plain unique index ignores case.
        Create.Index("ux_users_email").OnTable("users").OnColumn("email").Unique();

        Create.Table("sessions")
            .WithColumn("id").AsGuid().PrimaryKey("pk_sessions")
            .WithColumn("user_id").AsGuid().NotNullable().ForeignKey("fk_sessions_users", "users", "id")
            .WithColumn("security_stamp").AsGuid().NotNullable()
            .WithColumn("token_hash").AsString(64).NotNullable()
            .WithColumn("expires_at").AsCustom(Timestamp).NotNullable()
            .WithColumn("revoked_at").AsCustom(Timestamp).Nullable()
            .WithColumn("version").AsInt32().NotNullable();

        Create.Index("ix_sessions_user").OnTable("sessions").OnColumn("user_id");
    }

    public override void Down()
    {
        Delete.Table("sessions");
        Delete.Table("users");
    }
}
