using System.ComponentModel.DataAnnotations;

namespace TaskTickets.Api.Common;

public sealed class AuthRateLimitOptions
{
    [Range(1, 10000)]
    public int PerIp { get; set; } = 10;
    [Range(1, 100000)]
    public int Global { get; set; } = 100;
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}
