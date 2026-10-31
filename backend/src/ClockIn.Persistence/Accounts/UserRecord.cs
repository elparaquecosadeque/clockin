using LinqToDB.Mapping;

namespace ClockIn.Persistence.Accounts;

[Table("users")]
public sealed class UserRecord
{
    [Column("id"), PrimaryKey]
    public required Guid Id { get; init; }

    [Column("email")]
    public required string Email { get; init; }

    [Column("name")]
    public required string Name { get; init; }

    [Column("role")]
    public required string Role { get; init; }

    [Column("status")]
    public required string Status { get; init; }

    [Column("password_hash")]
    public required string? PasswordHash { get; init; }

    [Column("security_stamp")]
    public required Guid SecurityStamp { get; init; }

    [Column("failed_sign_ins")]
    public required int FailedSignIns { get; init; }

    [Column("locked_until")]
    public required DateTimeOffset? LockedUntil { get; init; }

    [Column("access_link_hash")]
    public required string? AccessLinkHash { get; init; }

    [Column("access_link_expires_at")]
    public required DateTimeOffset? AccessLinkExpiresAt { get; init; }

    [Column("team_id")]
    public required Guid? TeamId { get; init; }

    [Column("language")]
    public required string Language { get; init; }

    [Column("time_format")]
    public required string TimeFormat { get; init; }

    [Column("version")]
    public required int Version { get; init; }
}
