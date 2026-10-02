using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Shared.Data.Seed;

namespace MiniMes.Api.Shared.Data;

public static class DatabaseStartup
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await scope.ServiceProvider.GetRequiredService<MesDbContext>().Database.MigrateAsync();
        }

        if (app.Configuration.GetValue<bool>("Database:Seed"))
        {
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(CancellationToken.None);
        }
    }
}
