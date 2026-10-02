using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Data.Seed;
using Testcontainers.PostgreSql;

namespace MiniMes.IntegrationTests;

public sealed class MesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string[] ModuleSchemas = ["identity", "wo", "lot", "carrier", "eqp", "exec"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Mes"] = _postgres.GetConnectionString(),
            ["Database:MigrateOnStartup"] = "true",
            ["Database:Seed"] = "false",
            ["Seed:DemoPassword"] = "test-pass",
            ["Demo:EnableQuickLogin"] = "true"
        }));

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();

        var tables = await db.Database
            .SqlQuery<string>($"""
                SELECT format('%I.%I', table_schema, table_name) AS "Value"
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema = ANY ({ModuleSchemas})
                  AND table_name <> '__EFMigrationsHistory'
                """)
            .ToListAsync();

        if (tables.Count > 0)
        {
            // Table names come from information_schema and are already quoted by format('%I').
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"TRUNCATE {string.Join(", ", tables)} RESTART IDENTITY CASCADE");
#pragma warning restore EF1002
        }

        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(CancellationToken.None);
    }
}
