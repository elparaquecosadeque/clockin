namespace TaskTickets.Api.Features.Auth;

public sealed record User(Guid Id, string DisplayName, string Email, string NormalizedEmail, string PasswordHash);
