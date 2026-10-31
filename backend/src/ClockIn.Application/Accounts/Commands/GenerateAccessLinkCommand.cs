using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>An activation link for a pending user, a reset link for an active one. Admins only (permission policy).</summary>
public sealed record GenerateAccessLinkCommand(Guid UserId);

public sealed class GenerateAccessLinkHandler(ICurrentUser currentUser, IUserRepository usersRepository, IAuditLog audit, TimeProvider time)
{
    public async Task<AccessLinkDto> HandleAsync(GenerateAccessLinkCommand command, CancellationToken ct)
    {
        User user = await usersRepository.FindAsync(new UserId(command.UserId), ct) ?? throw new NotFoundException("User not found.");
        AccessLink link = user.IssueAccessLink(time.GetUtcNow());
        await usersRepository.SaveOrUpdateAsync(user, ct);
        audit.AccessLinkGenerated(currentUser.Id, user.Id);
        return new AccessLinkDto(link.Token, link.ExpiresAt);
    }
}
