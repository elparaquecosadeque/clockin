using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Tests.Common;

namespace ClockIn.Api.Tests.Accounts;

[Collection(SharedApi.Name)]
public sealed class SessionRepositoryTests(ApiFactory factory) : IDisposable
{
    private static readonly DateTimeOffset Now = CompanyClock.At("2026-10-30 10:00");

    private readonly Scopes _scopes = new(factory);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _scopes.Dispose();

    [Fact]
    public async Task Loads_what_was_saved_and_keeps_it_working_after_a_refresh()
    {
        User eve = await SavedUserAsync();
        (Session session, RefreshToken token) = Session.Start(eve.Id, eve.SecurityStamp, Now);
        await Sessions().SaveAsync(session, Ct);

        Session loaded = (await Sessions().FindAsync(session.Id, Ct))!;
        loaded.Should().BeEquivalentTo(session, o => o.Excluding(s => s.Version));
        (RefreshOutcome outcome, RefreshToken? next) = loaded.Refresh(token.Secret, Now.AddHours(1));
        await Sessions().SaveAsync(loaded, Ct);

        Session reloaded = (await Sessions().FindAsync(session.Id, Ct))!;
        outcome.Should().Be(RefreshOutcome.Refreshed);
        reloaded.Version.Should().Be(2);
        reloaded.Matches(next!.Secret).Should().BeTrue();
    }

    [Fact]
    public async Task Revokes_every_session_of_the_user_and_no_one_else()
    {
        User eve = await SavedUserAsync();
        User ed = await SavedUserAsync();
        (Session phone, _) = Session.Start(eve.Id, eve.SecurityStamp, Now);
        (Session laptop, _) = Session.Start(eve.Id, eve.SecurityStamp, Now);
        (Session other, _) = Session.Start(ed.Id, ed.SecurityStamp, Now);
        foreach (Session session in new[] { phone, laptop, other })
        {
            await Sessions().SaveAsync(session, Ct);
        }

        await Sessions().RevokeAllAsync(eve.Id, Now.AddMinutes(5), Ct);

        (await Sessions().FindAsync(phone.Id, Ct))!.RevokedAt.Should().Be(Now.AddMinutes(5));
        (await Sessions().FindAsync(laptop.Id, Ct))!.RevokedAt.Should().Be(Now.AddMinutes(5));
        (await Sessions().FindAsync(other.Id, Ct))!.RevokedAt.Should().BeNull();
    }

    private async Task<User> SavedUserAsync()
    {
        var user = User.Create(new Email($"{Guid.CreateVersion7():N}@clockin.test"), "Someone", Role.Employee);
        user.SetPassword("hash");
        await _scopes.Get<IUserRepository>().SaveAsync(user, Ct);
        return user;
    }

    private ISessionRepository Sessions() => _scopes.Get<ISessionRepository>();
}
