using Ardalis.GuardClauses;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Teams;

/// <summary>A named group with exactly one supervisor and any number of employees. Archived, never deleted.</summary>
/// <remarks>
/// Unique names and "a supervisor leads at most one team" span teams, so the handler checks them
/// against the repository, and unique indexes guarantee them under concurrency.
/// </remarks>
public sealed class Team
{
    public const int MaxNameLength = 100;

    private Team(TeamId id, string name, UserId supervisorId)
    {
        Id = id;
        Name = name;
        SupervisorId = supervisorId;
    }

    public TeamId Id { get; }

    public string Name { get; }

    public UserId SupervisorId { get; }

    public bool IsArchived { get; private init; }

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public static Team Create(string name, User supervisor)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.StringTooLong(name.Trim(), MaxNameLength);
        if (supervisor.Role != Role.Supervisor)
        {
            throw new DomainException("UserIsNotSupervisor", "Only a supervisor can lead a team.");
        }

        if (supervisor.Status == UserStatus.Deactivated)
        {
            throw new DomainException("UserIsDeactivated", "The user is deactivated.");
        }

        return new Team(TeamId.New(), name.Trim(), supervisor.Id);
    }

    /// <summary>Rebuilds a saved team. For repositories only; it checks no rules.</summary>
    public static Team Restore(TeamId id, string name, UserId supervisorId, bool isArchived, int version) =>
        new(id, name, supervisorId) { IsArchived = isArchived, Version = version };

    /// <summary>The form names are compared in: "Warehouse", "warehouse" and " Warehouse " are the same team.</summary>
    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
