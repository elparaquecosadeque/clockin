using ClockIn.Application.Accounts.Results;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Mappings;

public static class UserMappings
{
    public static MeDto ToMeDto(this User user) =>
        new(user.Id.Value, user.Name, user.Email.Value, user.Role, Permissions.For(user.Role));
}
