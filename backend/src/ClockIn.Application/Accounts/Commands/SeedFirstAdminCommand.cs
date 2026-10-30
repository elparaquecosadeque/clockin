using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Runs at startup so somebody can create everyone else (seed-first-admin.feature).</summary>
/// <param name="Email">The <c>FirstAdmin:Email</c> deploy setting; ignored once any admin is active.</param>
public sealed record SeedFirstAdminCommand(string? Email);

public sealed class SeedFirstAdminHandler(IUserRepository users, IAuditLog audit, TimeProvider time)
{
    public async Task HandleAsync(SeedFirstAdminCommand command, CancellationToken ct)
    {
        IReadOnlyList<User> admins = await users.FindByRoleAsync(Role.Admin, ct);
        if (admins.Any(a => a.Status == UserStatus.Active))
        {
            return;
        }

        User admin = admins.FirstOrDefault(a => a.Status == UserStatus.PendingActivation) ?? NewAdmin(command.Email);
        AccessLink link = admin.IssueAccessLink(time.GetUtcNow());
        await users.SaveAsync(admin, ct);
        audit.FirstAdminLinkIssued(admin.Email, link.Token);
    }

    private static User NewAdmin(string? email) =>
        string.IsNullOrWhiteSpace(email)
            ? throw new InvalidOperationException("No admin exists and the 'FirstAdmin:Email' setting is empty. Set it in the deploy config.")
            : User.Create(new Email(email), "Admin", Role.Admin);
}
