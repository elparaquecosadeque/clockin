using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Common.Interfaces;

/// <summary>The signed-in user of the current request.</summary>
public interface ICurrentUser
{
    UserId Id { get; }
}
