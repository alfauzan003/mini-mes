using System.Net.Http.Json;
using System.Text.Json;

namespace MiniMes.IntegrationTests;

[Collection("api")]
public class HealthTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Health_reports_database_up()
    {
        var body = await api.CreateClient().GetFromJsonAsync<JsonElement>(
            "/api/health", TestContext.Current.CancellationToken);
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("up", body.GetProperty("database").GetString());
    }
}
