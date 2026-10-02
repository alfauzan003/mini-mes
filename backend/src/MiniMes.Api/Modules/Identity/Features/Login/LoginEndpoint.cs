using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Identity.Features.Login;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, string Username, string DisplayName, Role Role, DateTimeOffset ExpiresAt)
{
    public static LoginResponse For(User user, IssuedToken issued) =>
        new(issued.Token, user.Username, user.DisplayName, user.Role, issued.ExpiresAt);

    public static Error InvalidCredentials { get; } = new(
        ErrorCodes.AuthInvalidCredentials, "Invalid username or password.", ErrorKind.Unauthorized);
}

public static class LoginEndpoint
{
    public static void MapLogin(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", async (
            LoginRequest request, MesDbContext db, JwtTokenService tokens, CancellationToken ct) =>
        {
            var user = string.IsNullOrWhiteSpace(request.Username)
                ? null
                : await db.Set<User>().SingleOrDefaultAsync(u => u.Username == request.Username, ct);

            Result<LoginResponse> result =
                user is not null && VerifyPassword(user, request.Password)
                    ? LoginResponse.For(user, tokens.Issue(user))
                    : LoginResponse.InvalidCredentials;

            return result.ToHttpResult();
        }).AllowAnonymous();

    private static bool VerifyPassword(User user, string? password) =>
        !string.IsNullOrEmpty(password)
        && new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, password)
            != PasswordVerificationResult.Failed;
}
