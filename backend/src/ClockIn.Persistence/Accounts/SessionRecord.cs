using LinqToDB.Mapping;

namespace ClockIn.Persistence.Accounts;

[Table("sessions")]
public sealed class SessionRecord
{
    [Column("id"), PrimaryKey]
    public required Guid Id { get; init; }

    [Column("user_id")]
    public required Guid UserId { get; init; }

    [Column("security_stamp")]
    public required Guid SecurityStamp { get; init; }

    [Column("token_hash")]
    public required string TokenHash { get; init; }

    [Column("expires_at")]
    public required DateTimeOffset ExpiresAt { get; init; }

    [Column("revoked_at")]
    public required DateTimeOffset? RevokedAt { get; init; }

    [Column("version")]
    public required int Version { get; init; }
}
