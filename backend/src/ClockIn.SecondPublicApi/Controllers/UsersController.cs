using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Results;
using ClockIn.Hosting.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockIn.SecondPublicApi.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = PoliciesConstants.ManageUsers)]
public sealed class UsersController(CreateUserHandler handler, CreateUserValidator validator) : ControllerBase
{
    /// <summary>Create a pending account and return its activation token. Employees require an active teamId.</summary>
    [HttpPost]
    public async Task<AccessLinkDto> CreateAsync(CreateUserCommand request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return await handler.HandleAsync(request, ct);
    }
}
