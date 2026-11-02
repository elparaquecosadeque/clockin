namespace TaskTickets.Api.Features.Auth;

public sealed record UserResponse(Guid Id, string DisplayName, string Email);
