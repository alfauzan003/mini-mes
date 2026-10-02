using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiniMes.IntegrationTests.Identity;

[Collection("api")]
public class AuthTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Login_with_demo_password_returns_token_and_role()
    {
        var response = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { username = "planner", password = "test-pass" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("PLANNER", body.GetProperty("role").GetString());
        Assert.Equal("Demo Planner", body.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_invalid_credentials()
    {
        var response = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { username = "planner", password = "nope" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var response = await api.CreateClient().GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_token_returns_current_user()
    {
        var client = await api.ClientAsAsync("qc");

        var body = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal("qc", body.GetProperty("username").GetString());
        Assert.Equal("QC", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Demo_login_returns_token_without_password()
    {
        var response = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/demo-login", new { username = "operator" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("OPERATOR", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Demo_login_is_not_mapped_when_disabled()
    {
        await using var disabled = api.WithWebHostBuilder(b => b.UseSetting("Demo:EnableQuickLogin", "false"));

        var response = await disabled.CreateClient().PostAsJsonAsync(
            "/api/auth/demo-login", new { username = "operator" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
