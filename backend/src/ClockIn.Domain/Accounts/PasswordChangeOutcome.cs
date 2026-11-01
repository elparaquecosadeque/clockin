namespace ClockIn.Domain.Accounts;

public enum PasswordChangeOutcome
{
    Changed,
    Rejected,

    /// <summary>Rejected, and this attempt locked the account.</summary>
    LockedOut,
}
