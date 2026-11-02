using System.ComponentModel.DataAnnotations;

namespace TaskTickets.Api.Common;

public sealed class AuthOptions
{
    [Required]
    public string Issuer { get; set; } = "TaskTickets";
    [Required]
    public string Audience { get; set; } = "TaskTickets.Client";
    [Range(1, 30)]
    public int LifetimeMinutes { get; set; } = 30;
}
