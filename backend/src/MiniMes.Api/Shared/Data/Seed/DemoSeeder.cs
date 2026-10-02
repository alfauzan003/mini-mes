using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;

namespace MiniMes.Api.Shared.Data.Seed;

public class DemoSeeder(MesDbContext db, IConfiguration configuration)
{
    private static readonly (string Username, string DisplayName, Role Role)[] DemoUsers =
    [
        ("planner", "Demo Planner", Role.Planner),
        ("operator", "Demo Operator", Role.Operator),
        ("qc", "Demo QC", Role.QC),
        ("admin", "Demo Admin", Role.Admin)
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        await SeedUsersAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("Seed:DemoPassword must be set to seed demo users.");
        }

        var existing = await db.Set<User>().Select(u => u.Username).ToListAsync(ct);
        var hasher = new PasswordHasher<User>();
        foreach (var (username, displayName, role) in DemoUsers.Where(u => !existing.Contains(u.Username)))
        {
            var user = new User { Username = username, DisplayName = displayName, Role = role };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Set<User>().Add(user);
        }
    }
}
