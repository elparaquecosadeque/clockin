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

    public string Name { get; private set; }

    public UserId SupervisorId { get; private set; }

    public bool IsArchived { get; private set; }

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public static Team Create(string name, User supervisor)
    {
        EnsureCanLead(supervisor);
        return new Team(TeamId.New(), CheckedName(name), supervisor.Id);
    }

    /// <remarks>The handler checks that no other team has the name; changing only the case is fine.</remarks>
    public void Rename(string name)
    {
        EnsureActive();
        Name = CheckedName(name);
    }

    /// <summary>Pending work follows the team, since the approver is derived from it.</summary>
    /// <remarks>The handler checks that the new supervisor leads no other team.</remarks>
    public void ChangeSupervisor(User supervisor)
    {
        EnsureActive();
        EnsureCanLead(supervisor);
        SupervisorId = supervisor.Id;
    }

    /// <remarks>The handler checks that no active employee or pending review is left in the team.</remarks>
    public void Archive()
    {
        EnsureActive();
        IsArchived = true;
    }

    /// <summary>Rebuilds a saved team. For repositories only; it checks no rules.</summary>
    public static Team Restore(TeamId id, string name, UserId supervisorId, bool isArchived, int version) =>
        new(id, name, supervisorId) { IsArchived = isArchived, Version = version };

    private static string CheckedName(string name)
    {
        Guard.Against.NullOrWhiteSpace(name);
        return Guard.Against.StringTooLong(name.Trim(), MaxNameLength);
    }

    private static void EnsureCanLead(User supervisor)
    {
        if (supervisor.Role != Role.Supervisor)
        {
            throw new DomainException("UserIsNotSupervisor", "Only a supervisor can lead a team.");
        }

        if (supervisor.Status == UserStatus.Deactivated)
        {
            throw new DomainException("UserIsDeactivated", "The user is deactivated.");
        }
    }

    private void EnsureActive()
    {
        if (IsArchived)
        {
            throw new DomainException("TeamIsArchived", "The team is archived.");
        }
    }

    /// <summary>The form names are compared in: "Warehouse", "warehouse" and " Warehouse " are the same team.</summary>
    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
