using Ardalis.GuardClauses;

namespace ClockIn.Domain.Timesheets;

/// <summary>Where a punch came from, kept as evidence for supervisors.</summary>
public sealed record PunchSource
{
    public const int MaxUserAgentLength = 512;

    public PunchSource(string ipAddress, string userAgent)
    {
        IpAddress = Guard.Against.NullOrWhiteSpace(ipAddress).Trim();
        // A long user agent is not a reason to lose a punch: keep the first part.
        UserAgent = userAgent.Length > MaxUserAgentLength ? userAgent[..MaxUserAgentLength] : userAgent;
    }

    public string IpAddress { get; }

    public string UserAgent { get; }
}
