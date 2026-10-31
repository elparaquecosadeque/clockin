using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Reopens the timesheet to Draft, through CorrectionRequestAccepted in the same transaction.</summary>
public sealed record AcceptCorrectionCommand(Guid RequestId, string? Comment) : ITransactional;

public sealed class AcceptCorrectionValidator : AbstractValidator<AcceptCorrectionCommand>
{
    public AcceptCorrectionValidator() =>
        RuleFor(c => c.Comment).Must(c => c is null || c.Trim().Length <= Timesheet.MaxCommentLength)
            .WithMessage($"Use at most {Timesheet.MaxCommentLength} characters.");
}

public sealed class AcceptCorrectionHandler(
    ICurrentUser currentUser, IUserRepository users, ITeamRepository teams, ICorrectionRequestRepository corrections, TimeProvider time)
{
    public async Task HandleAsync(AcceptCorrectionCommand command, CancellationToken ct)
    {
        CorrectionRequest request = await corrections.GetForApproverAsync(command.RequestId, currentUser, users, teams, ct);
        request.Accept(currentUser.Id, command.Comment, time.GetUtcNow());
        await corrections.SaveAsync(request, ct);
    }
}
