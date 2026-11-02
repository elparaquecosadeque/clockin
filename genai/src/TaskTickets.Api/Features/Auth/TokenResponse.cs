namespace TaskTickets.Api.Features.Auth;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
