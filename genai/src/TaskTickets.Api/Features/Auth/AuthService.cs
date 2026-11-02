using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using TaskTickets.Api.Common;
using TaskTickets.Api.Data;

namespace TaskTickets.Api.Features.Auth;

public sealed class AuthService(InMemoryStore store, PasswordHasher<User> hasher, TokenIssuer tokens, DummyPassword dummy)
{
    public UserResponse Register(RegisterRequest request, Guid? seedId = null)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        var user = new User(seedId ?? Guid.CreateVersion7(), request.DisplayName, request.Email, request.Email.ToUpperInvariant(), "");
        user = user with { PasswordHash = hasher.HashPassword(user, request.Password) };
        store.AddUser(user);
        return new UserResponse(user.Id, user.DisplayName, user.Email);
    }

    public TokenResponse Login(LoginRequest request)
    {
        var user = store.FindUserByEmail(request.Email.ToUpperInvariant());
        var candidate = user ?? dummy.User;
        var result = hasher.VerifyHashedPassword(candidate, candidate.PasswordHash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
        {
            throw new ApiException(401, "Invalid email or password.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            store.UpdatePasswordHash(user.Id, user.PasswordHash, hasher.HashPassword(user, request.Password));
        }

        return tokens.Issue(user);
    }

    public UserResponse Me(Guid id)
    {
        var user = store.FindUser(id) ?? throw new ApiException(401, "Invalid user.");
        return new UserResponse(user.Id, user.DisplayName, user.Email);
    }
}
