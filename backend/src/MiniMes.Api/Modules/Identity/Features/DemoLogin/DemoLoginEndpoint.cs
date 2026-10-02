using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity.Features.Login;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Identity.Features.DemoLogin;

public sealed record DemoLoginRequest(string Username);

public static class DemoLoginEndpoint
{
    public static void MapDemoLogin(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/demo-login", async (
            DemoLoginRequest request, MesDbContext db, JwtTokenService tokens, CancellationToken ct) =>
        {
            var user = string.IsNullOrWhiteSpace(request.Username)
                ? null
                : await db.Set<User>().SingleOrDefaultAsync(u => u.Username == request.Username, ct);

            Result<LoginResponse> result = user is not null
                ? LoginResponse.For(user, tokens.Issue(user))
                : LoginResponse.InvalidCredentials;

            return result.ToHttpResult();
        }).AllowAnonymous();
}
