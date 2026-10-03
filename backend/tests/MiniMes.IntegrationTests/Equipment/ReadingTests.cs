using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Parameters;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.IntegrationTests.Equipment;

[Collection("api")]
public class ReadingTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _factory = api.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_time));
            // A private instance, so latest-value and throttle state never leaks between tests.
            s.Replace(ServiceDescriptor.Singleton(new LatestReadings()));
        }));
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<IReadOnlyList<LiveReadingDto>>> RecordAsync(string code, params ReadingInput[] inputs)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReadingRecorder>().RecordAsync(code, inputs, Ct);
    }

    private async Task<int> RowCountAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MesDbContext>()
            .Set<ParameterReading>().CountAsync(Ct);
    }

    [Fact]
    public async Task Recording_updates_latest_and_persists_first_sample()
    {
        var result = await RecordAsync("MX01", new ReadingInput("Slurry temp", 24.5m));

        Assert.True(result.IsSuccess, result.Error?.Code);
        var live = Assert.Single(_factory.Services.GetRequiredService<LatestReadings>().All());
        Assert.Equal("MX01", live.EquipmentCode);
        Assert.Equal(24.5m, live.Value);
        Assert.Equal(20m, live.Low);
        Assert.Equal(30m, live.High);
        Assert.Equal(Start, live.At);
        Assert.Equal(1, await RowCountAsync());

        var client = await _factory.ClientAsAsync("operator");
        var latest = await client.GetFromJsonAsync<JsonElement>("/api/readings/latest", Ct);
        Assert.Equal("Slurry temp", Assert.Single(latest.EnumerateArray()).GetProperty("parameter").GetString());
    }

    [Fact]
    public async Task Second_reading_within_ten_seconds_is_not_persisted()
    {
        await RecordAsync("MX01", new ReadingInput("Slurry temp", 24m));
        _time.Advance(TimeSpan.FromSeconds(2));
        await RecordAsync("MX01", new ReadingInput("Slurry temp", 25m));

        Assert.Equal(1, await RowCountAsync());
        Assert.Equal(25m, Assert.Single(_factory.Services.GetRequiredService<LatestReadings>().All()).Value);

        _time.Advance(TimeSpan.FromSeconds(10));
        await RecordAsync("MX01", new ReadingInput("Slurry temp", 26m));

        Assert.Equal(2, await RowCountAsync());
    }

    [Fact]
    public async Task Unknown_parameter_is_rejected()
    {
        var result = await RecordAsync("MX01", new ReadingInput("Line speed", 40m));
        var unknownEquipment = await RecordAsync("NOPE", new ReadingInput("Slurry temp", 24m));

        Assert.Equal("UNKNOWN_PARAMETER", result.Error?.Code);
        Assert.Equal("EQUIPMENT_NOT_FOUND", unknownEquipment.Error?.Code);
        Assert.Equal(0, await RowCountAsync());
        Assert.Empty(_factory.Services.GetRequiredService<LatestReadings>().All());
    }

    [Fact]
    public async Task Unfittable_value_is_invalid_quantity()
    {
        var result = await RecordAsync("MX01", new ReadingInput("Slurry temp", 24.12345m));

        Assert.Equal("INVALID_QUANTITY", result.Error?.Code);
    }

    [Fact]
    public async Task Series_returns_points_in_range_per_parameter()
    {
        await RecordAsync("MX01", new ReadingInput("Slurry temp", 24m), new ReadingInput("Vacuum", 85m));
        _time.Advance(TimeSpan.FromMinutes(1));
        await RecordAsync("MX01", new ReadingInput("Slurry temp", 25m));
        _time.Advance(TimeSpan.FromMinutes(1));
        await RecordAsync("MX01", new ReadingInput("Slurry temp", 26m));
        var client = await _factory.ClientAsAsync("operator");

        var series = await client.GetFromJsonAsync<JsonElement>("/api/equipment/MX01/parameters", Ct);

        Assert.Equal(
            ["Slurry temp", "Agitator speed", "Vacuum"],
            series.EnumerateArray().Select(s => s.GetProperty("parameter").GetString()));
        Assert.Equal(
            [24m, 25m, 26m],
            series[0].GetProperty("points").EnumerateArray().Select(p => p.GetProperty("value").GetDecimal()));
        Assert.Equal(0, series[1].GetProperty("points").GetArrayLength());
        Assert.Equal(1, series[2].GetProperty("points").GetArrayLength());
        Assert.Equal("TEMPERATURE", series[0].GetProperty("kind").GetString());
        Assert.Equal(20m, series[0].GetProperty("low").GetDecimal());

        var from = Uri.EscapeDataString(Start.AddSeconds(30).ToString("O"));
        var to = Uri.EscapeDataString(Start.AddSeconds(90).ToString("O"));
        var narrowed = await client.GetFromJsonAsync<JsonElement>(
            $"/api/equipment/MX01/parameters?from={from}&to={to}", Ct);
        Assert.Equal(
            [25m],
            narrowed[0].GetProperty("points").EnumerateArray().Select(p => p.GetProperty("value").GetDecimal()));
    }

    [Fact]
    public async Task Series_for_unknown_equipment_is_404()
    {
        var client = await _factory.ClientAsAsync("operator");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/equipment/NOPE/parameters", Ct)).StatusCode);
    }

    [Fact]
    public async Task Range_over_24h_is_invalid_date_range()
    {
        var client = await _factory.ClientAsAsync("operator");
        var from = Uri.EscapeDataString(Start.AddHours(-25).ToString("O"));

        var response = await client.GetAsync($"/api/equipment/MX01/parameters?from={from}", Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("INVALID_DATE_RANGE", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task From_after_to_is_invalid_date_range()
    {
        var client = await _factory.ClientAsAsync("operator");
        var from = Uri.EscapeDataString(Start.ToString("O"));
        var to = Uri.EscapeDataString(Start.AddHours(-1).ToString("O"));

        var response = await client.GetAsync($"/api/equipment/MX01/parameters?from={from}&to={to}", Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("INVALID_DATE_RANGE", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Purge_deletes_only_readings_older_than_retention()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
            var equipmentId = await db.Set<EquipmentEntity>()
                .Where(e => e.Code == "MX01").Select(e => e.Id).SingleAsync(Ct);
            db.Set<ParameterReading>().AddRange(
                new ParameterReading(equipmentId, "Slurry temp", 24m, Start.AddDays(-8)),
                new ParameterReading(equipmentId, "Slurry temp", 25m, Start.AddDays(-6)));
            await db.SaveChangesAsync(Ct);
        }

        int deleted;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            deleted = await scope.ServiceProvider.GetRequiredService<ReadingRetention>().PurgeAsync(Ct);
        }

        Assert.Equal(1, deleted);
        Assert.Equal(1, await RowCountAsync());
    }
}
