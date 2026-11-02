using System.ComponentModel.DataAnnotations;

namespace TaskTickets.Api.Features.Auth;

public sealed record RegisterRequest(string DisplayName, string Email, string Password)
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string DisplayName { get; init; } = DisplayName?.Trim()!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = Email?.Trim()!;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; init; } = Password;
}
