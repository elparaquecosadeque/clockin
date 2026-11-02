using System.ComponentModel.DataAnnotations;

namespace TaskTickets.Api.Features.Auth;

public sealed record LoginRequest(string Email, [property: Required, StringLength(128)] string Password)
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = Email?.Trim()!;
}
