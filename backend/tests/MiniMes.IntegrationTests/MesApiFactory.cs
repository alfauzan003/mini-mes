using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Data.Seed;
using Testcontainers.PostgreSql;

namespace MiniMes.IntegrationTests;

public sealed class MesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string[] ModuleSchemas = ["identity", "wo", "lot", "carrier", "eqp", "exec", "qc", "alarm"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // UseSetting (host configuration) rather than an in-memory source, so a derived
    // factory's UseSetting(...) can still override these defaults.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Mes", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:Seed", "false");
        builder.UseSetting("Seed:DemoPassword", "test-pass");
        builder.UseSetting("Seed:InspectionSpecs", "false");
        builder.UseSetting("Demo:EnableQuickLogin", "true");
        // Keeps the retention service's startup purge from racing tests that insert old rows.
        builder.UseSetting("Parameters:RetentionInitialDelaySeconds", "3600");
    }

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

    /// <summary>Seeds the demo inspection specs, which the default test host leaves out.</summary>
    public async Task SeedInspectionSpecsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedInspectionSpecsAsync(CancellationToken.None);
    }
}

public static class ApiFactoryExtensions
{
    /// <summary>
    /// A client logged in as a seeded user. An extension on the base factory so it also works on the
    /// derived factory <c>WithWebHostBuilder</c> returns.
    /// </summary>
    public static async Task<HttpClient> ClientAsAsync(this WebApplicationFactory<Program> factory, string username)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.LoginTokenAsync(username));
        return client;
    }

    public static async Task<string> LoginTokenAsync(this WebApplicationFactory<Program> factory, string username)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { username, password = "test-pass" }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("token").GetString()!;
    }

    /// <summary>
    /// A started connection to the shop floor hub, signed in as a seeded user, or anonymous when
    /// <paramref name="username"/> is null. Long polling, because the in-memory test server has no WebSockets.
    /// </summary>
    public static async Task<HubConnection> ConnectShopfloorAsync(
        this WebApplicationFactory<Program> factory, string? username)
    {
        var token = username is null ? null : await factory.LoginTokenAsync(username);
        var server = factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/shopfloor"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)))
            .Build();

        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }
}
