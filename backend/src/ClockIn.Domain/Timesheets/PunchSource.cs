using Ardalis.GuardClauses;

namespace ClockIn.Domain.Timesheets;

/// <summary>Where a punch came from, kept as evidence for supervisors.</summary>
public sealed record PunchSource
{
    public const int MaxUserAgentLength = 512;

    public PunchSource(string ipAddress, string userAgent)
    {
        // An IPv6 zone (fe80::1%12) names the server's network interface, not the client, and can push the address
        // past the 45-character column.
        IpAddress = Guard.Against.NullOrWhiteSpace(ipAddress).Trim().Split('%')[0];
        // A long user agent is not a reason to lose a punch: keep the first part.
        UserAgent = userAgent.Length > MaxUserAgentLength ? userAgent[..MaxUserAgentLength] : userAgent;
    }

    public string IpAddress { get; }

    public string UserAgent { get; }
}
