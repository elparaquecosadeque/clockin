namespace ClockIn.Domain.Accounts;

public enum RefreshOutcome
{
    Refreshed,

    /// <summary>Expired or revoked.</summary>
    Rejected,

    /// <summary>An already-rotated token came back: it was probably stolen.</summary>
    Reused,
}
