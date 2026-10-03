using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using MiniMes.Api.Modules.Alarms;

namespace MiniMes.IntegrationTests.Alarms;

[Collection("api")]
public class AlarmEndpointTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> RaiseAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string equipment, string code)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AlarmService>().RaiseAsync(equipment, code, Ct);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value;
    }

    private static async Task ClearAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string equipment, string code)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AlarmService>().ClearAsync(equipment, code, Ct);
        Assert.True(result.IsSuccess, result.Error?.Code);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task Active_filter_lists_only_uncleared()
    {
        await RaiseAsync(api, "MX01", "MX-VAC-LOW");
        await RaiseAsync(api, "MX01", "MX-TEMP-HIGH");
        await ClearAsync(api, "MX01", "MX-VAC-LOW");
        var client = await api.ClientAsAsync("operator");

        var active = await BodyAsync(await client.GetAsync("/api/alarms?active=true", Ct));
        var cleared = await BodyAsync(await client.GetAsync("/api/alarms?active=false", Ct));
        var all = await BodyAsync(await client.GetAsync("/api/alarms", Ct));

        Assert.Equal(["MX-TEMP-HIGH"], active.EnumerateArray().Select(a => a.GetProperty("code").GetString()));
        Assert.Equal(["MX-VAC-LOW"], cleared.EnumerateArray().Select(a => a.GetProperty("code").GetString()));
        Assert.Equal(2, all.GetArrayLength());
        Assert.Equal("MX01", active[0].GetProperty("equipmentCode").GetString());
        Assert.Equal("Slurry temperature high", active[0].GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, active[0].GetProperty("durationSeconds").ValueKind);
    }

    [Fact]
    public async Task Cleared_alarm_has_duration()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero));
        using var factory = api.WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(time))));
        await RaiseAsync(factory, "MX01", "MX-VAC-LOW");
        time.Advance(TimeSpan.FromSeconds(90));
        await ClearAsync(factory, "MX01", "MX-VAC-LOW");
        var client = await factory.ClientAsAsync("operator");

        var list = await BodyAsync(await client.GetAsync("/api/alarms?active=false", Ct));

        Assert.Equal(90, list[0].GetProperty("durationSeconds").GetDouble());
    }

    [Fact]
    public async Task Operator_acknowledges_alarm()
    {
        var id = await RaiseAsync(api, "MX01", "MX-VAC-LOW");
        var client = await api.ClientAsAsync("operator");

        var response = await client.PostAsync($"/api/alarms/{id}/acknowledge", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("operator", body.GetProperty("acknowledgedBy").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("acknowledgedAt").ValueKind);
    }

    [Fact]
    public async Task Second_acknowledge_is_422()
    {
        var id = await RaiseAsync(api, "MX01", "MX-VAC-LOW");
        var client = await api.ClientAsAsync("operator");
        await client.PostAsync($"/api/alarms/{id}/acknowledge", null, Ct);

        var response = await client.PostAsync($"/api/alarms/{id}/acknowledge", null, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ALARM_ALREADY_ACKNOWLEDGED", (await BodyAsync(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Planner_cannot_acknowledge()
    {
        var id = await RaiseAsync(api, "MX01", "MX-VAC-LOW");
        var client = await api.ClientAsAsync("planner");

        var response = await client.PostAsync($"/api/alarms/{id}/acknowledge", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_alarm_is_404()
    {
        var client = await api.ClientAsAsync("operator");

        var response = await client.PostAsync($"/api/alarms/{Guid.NewGuid()}/acknowledge", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ALARM_NOT_FOUND", (await BodyAsync(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Severity_filter_parses_screaming_snake()
    {
        await RaiseAsync(api, "MX01", "MX-VAC-LOW");
        await RaiseAsync(api, "CT01", "CT-WEB-BREAK");
        var client = await api.ClientAsAsync("operator");

        var list = await BodyAsync(await client.GetAsync("/api/alarms?severity=CRITICAL", Ct));

        Assert.Equal(["CT-WEB-BREAK"], list.EnumerateArray().Select(a => a.GetProperty("code").GetString()));
        Assert.Equal("CRITICAL", list[0].GetProperty("severity").GetString());
    }

    [Fact]
    public async Task Bad_filters_are_400_not_500()
    {
        var client = await api.ClientAsAsync("operator");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/alarms?severity=NOPE", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/alarms?from=garbage", Ct)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/alarms?from=2026-10-04T00:00:00Z&to=2026-10-03T00:00:00Z", Ct)).StatusCode);
    }
}
