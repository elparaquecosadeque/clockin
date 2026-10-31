using ClockIn.Application.Accounts.Results;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Mappings;

public static class UserMappings
{
    public static MeDto ToMeDto(this User user, string? teamName = null, string? supervisorName = null) =>
        new(user.Id.Value, user.Name, user.Email.Value, user.Role, Permissions.For(user.Role), user.Language, user.TimeFormat, teamName, supervisorName);
}
