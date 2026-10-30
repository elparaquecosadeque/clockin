using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Accounts;

/// <summary>refresh-session.feature and log-out.feature: the rules the Session aggregate owns.</summary>
public sealed class SessionTests
{
    private static readonly DateTimeOffset Now = CompanyClock.At("2026-10-30 10:00");
    private static readonly UserId Eve = UserId.New();
    private static readonly Guid Stamp = Guid.CreateVersion7();

    [Fact]
    public void Issues_a_refresh_token_valid_for_12_hours_when_started()
    {
        (Session session, RefreshToken token) = Session.Start(Eve, Stamp, Now);

        token.SessionId.Should().Be(session.Id);
        token.ExpiresAt.Should().Be(Now.AddHours(12));
        session.ExpiresAt.Should().Be(Now.AddHours(12));
        session.UserId.Should().Be(Eve);
        session.SecurityStamp.Should().Be(Stamp);
    }

    [Fact]
    public void Stores_only_a_hash_of_the_secret()
    {
        (Session session, RefreshToken token) = Session.Start(Eve, Stamp, Now);

        session.TokenHash.Should().NotContain(token.Secret);
    }

    [Fact]
    public void Round_trips_the_token_through_its_text_form()
    {
        (_, RefreshToken token) = Session.Start(Eve, Stamp, Now);

        RefreshToken.TryParse(token.Value, out SessionId sessionId, out string? secret).Should().BeTrue();

        sessionId.Should().Be(token.SessionId);
        secret.Should().Be(token.Secret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot")]
    [InlineData("not-a-guid.secret")]
    [InlineData("0193a1b2c3d47e8f9a0b1c2d3e4f5a6b.")]
    public void Rejects_a_malformed_token(string value)
    {
        RefreshToken.TryParse(value, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Rotates_the_token_and_slides_the_expiry_when_refreshed()
    {
        (Session session, RefreshToken first) = Session.Start(Eve, Stamp, Now);

        (RefreshOutcome outcome, RefreshToken? second) = session.Refresh(first.Secret, Now.AddHours(11));

        outcome.Should().Be(RefreshOutcome.Refreshed);
        second!.ExpiresAt.Should().Be(Now.AddHours(23));
        second.Secret.Should().NotBe(first.Secret);
        session.ExpiresAt.Should().Be(Now.AddHours(23));
    }

    [Fact]
    public void Keeps_refreshing_through_a_long_shift()
    {
        (Session session, RefreshToken token) = Session.Start(Eve, Stamp, Now);

        for (int quarter = 1; quarter <= 80; quarter++)
        {
            (RefreshOutcome outcome, RefreshToken? next) = session.Refresh(token.Secret, Now.AddMinutes(15 * quarter));
            outcome.Should().Be(RefreshOutcome.Refreshed);
            token = next!;
        }
    }

    [Fact]
    public void Rejects_the_token_when_expired()
    {
        (Session session, RefreshToken token) = Session.Start(Eve, Stamp, Now);

        session.Refresh(token.Secret, Now.AddHours(12).AddSeconds(1)).Outcome.Should().Be(RefreshOutcome.Rejected);
    }

    [Fact]
    public void Reports_reuse_when_a_rotated_token_is_used_again()
    {
        (Session session, RefreshToken first) = Session.Start(Eve, Stamp, Now);
        session.Refresh(first.Secret, Now.AddMinutes(15));

        session.Refresh(first.Secret, Now.AddMinutes(16)).Outcome.Should().Be(RefreshOutcome.Reused);
    }

    [Fact]
    public void Rejects_the_token_when_the_session_was_revoked()
    {
        (Session session, RefreshToken token) = Session.Start(Eve, Stamp, Now);

        session.Revoke(Now);

        session.Refresh(token.Secret, Now.AddMinutes(1)).Outcome.Should().Be(RefreshOutcome.Rejected);
    }

    [Fact]
    public void Matches_only_its_current_secret()
    {
        (Session session, RefreshToken token) = Session.Start(Eve, Stamp, Now);

        session.Matches(token.Secret).Should().BeTrue();
        session.Matches("something-else").Should().BeFalse();
    }
}
