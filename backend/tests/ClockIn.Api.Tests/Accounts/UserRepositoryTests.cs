using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Accounts;
using ClockIn.Tests.Common;

namespace ClockIn.Api.Tests.Accounts;

[Collection(SharedApi.Name)]
public sealed class UserRepositoryTests(ApiFactory factory) : IDisposable
{
    private readonly Scopes _scopes = new(factory);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _scopes.Dispose();

    [Fact]
    public async Task Loads_what_was_saved_when_found_by_id_or_email()
    {
        User eve = NewUser();
        eve.SetPassword("hash");
        eve.SignIn(passwordMatches: false, CompanyClock.At("2026-10-30 10:00"));
        await Users().SaveAsync(eve, Ct);

        User? byId = await Users().FindAsync(eve.Id, Ct);
        User? byEmail = await Users().FindByEmailAsync(eve.Email, Ct);

        byId.Should().BeEquivalentTo(eve, o => o.Excluding(u => u.Version));
        byId!.Version.Should().Be(1);
        byEmail!.Id.Should().Be(eve.Id);
    }

    [Fact]
    public async Task Saves_changes_and_bumps_the_version_when_the_user_exists()
    {
        User fay = NewUser();
        await Users().SaveAsync(fay, Ct);
        User loaded = (await Users().FindAsync(fay.Id, Ct))!;

        loaded.SetPassword("hash");
        await Users().SaveAsync(loaded, Ct);

        User reloaded = (await Users().FindAsync(fay.Id, Ct))!;
        reloaded.Status.Should().Be(UserStatus.Active);
        reloaded.PasswordHash.Should().Be("hash");
        reloaded.Version.Should().Be(2);
    }

    [Fact]
    public async Task Rejects_a_save_when_someone_else_saved_first()
    {
        User fay = NewUser();
        await Users().SaveAsync(fay, Ct);
        User first = (await Users().FindAsync(fay.Id, Ct))!;
        User second = (await Users().FindAsync(fay.Id, Ct))!;
        first.SetPassword("one");
        second.SetPassword("two");
        await Users().SaveAsync(first, Ct);

        var act = () => Users().SaveAsync(second, Ct);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("ConcurrencyConflict");
    }

    [Fact]
    public async Task Rejects_a_new_user_when_the_email_is_taken()
    {
        User eve = NewUser();
        await Users().SaveAsync(eve, Ct);

        var act = () => Users().SaveAsync(User.Create(new Email(eve.Email.Value.ToUpperInvariant()), "Eve 2", Role.Employee), Ct);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("EmailAlreadyInUse");
    }

    private static User NewUser() => User.Create(new Email($"{Guid.CreateVersion7():N}@clockin.test"), "Someone", Role.Employee);

    private IUserRepository Users() => _scopes.Get<IUserRepository>();
}
