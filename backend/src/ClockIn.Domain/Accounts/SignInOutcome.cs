namespace ClockIn.Domain.Accounts;

public enum SignInOutcome
{
    SignedIn,
    Rejected,

    /// <summary>Rejected, and this attempt locked the account.</summary>
    LockedOut,
}
