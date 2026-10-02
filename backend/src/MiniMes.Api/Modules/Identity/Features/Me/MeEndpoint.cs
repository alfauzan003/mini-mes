using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity.Features.Login;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Identity.Features.Me;

public sealed record UserDto(string Username, string DisplayName, Role Role);

public static class MeEndpoint
{
    public static void MapMe(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/auth/me", async (ICurrentUser current, MesDbContext db, CancellationToken ct) =>
        {
            var user = await db.Set<User>()
                .Where(u => u.Id == current.UserId)
                .Select(u => new UserDto(u.Username, u.DisplayName, u.Role))
                .SingleOrDefaultAsync(ct);

            Result<UserDto> result = user is not null ? user : LoginResponse.InvalidCredentials;
            return result.ToHttpResult();
        }).RequireAuthorization();
}
