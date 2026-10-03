using System.Net;
using System.Net.Http.Json;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Equipment.Features.StatusLog;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.IntegrationTests.Equipment;

[Collection("api")]
public class MaintenanceTests(MesApiFactory api) : IAsyncLifetime
{
    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_starts_and_ends_maintenance()
    {
        var admin = await api.ClientAsAsync("admin");

        var started = await admin.PostAsync("/api/equipment/MX01/maintenance/start", null, Ct);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var dto = await started.Content.ReadFromJsonAsync<EquipmentDto>(ProductionDriver.JsonOptions, Ct);
        Assert.Equal(EquipmentStatus.Maintenance, dto!.Status);

        var ended = await admin.PostAsync("/api/equipment/MX01/maintenance/end", null, Ct);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        dto = await ended.Content.ReadFromJsonAsync<EquipmentDto>(ProductionDriver.JsonOptions, Ct);
        Assert.Equal(EquipmentStatus.Idle, dto!.Status);
    }

    [Fact]
    public async Task Operator_cannot_start_maintenance()
    {
        var response = await (await _driver.OperatorAsync()).PostAsync("/api/equipment/MX01/maintenance/start", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ending_maintenance_when_idle_is_an_invalid_transition()
    {
        var admin = await api.ClientAsAsync("admin");

        var response = await admin.PostAsync("/api/equipment/MX01/maintenance/end", null, Ct);

        await ProductionDriver.AssertErrorAsync(response, 422, "EQUIPMENT_INVALID_TRANSITION");
    }

    [Fact]
    public async Task Unknown_equipment_maintenance_and_status_log_are_not_found()
    {
        var admin = await api.ClientAsAsync("admin");

        await ProductionDriver.AssertErrorAsync(
            await admin.PostAsync("/api/equipment/NOPE/maintenance/start", null, Ct), 404, "EQUIPMENT_NOT_FOUND");
        await ProductionDriver.AssertErrorAsync(
            await admin.GetAsync("/api/equipment/NOPE/status-log", Ct), 404, "EQUIPMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Track_in_during_maintenance_is_not_available()
    {
        var admin = await api.ClientAsAsync("admin");
        (await admin.PostAsync("/api/equipment/MX01/maintenance/start", null, Ct)).EnsureSuccessStatusCode();
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 422, "EQUIPMENT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Status_log_records_track_in_and_track_out()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");
        var run = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, lots);
        await _driver.ProduceOkAsync(run.Id, new OutputLine(null, null, 480m, 20m));
        await _driver.TrackOutOkAsync(run.Id);

        var log = await (await _driver.OperatorAsync())
            .GetFromJsonAsync<List<EquipmentStatusLogDto>>("/api/equipment/MX01/status-log", ProductionDriver.JsonOptions, Ct);

        Assert.Equal(2, log!.Count);
        Assert.Equal((EquipmentStatus.Running, EquipmentStatus.Idle, "Track-out"), (log[0].From, log[0].To, log[0].Reason));
        Assert.Equal((EquipmentStatus.Idle, EquipmentStatus.Running, "Track-in"), (log[1].From, log[1].To, log[1].Reason));
        Assert.True(log[0].ChangedAt >= log[1].ChangedAt);
    }

    [Fact]
    public async Task Status_log_limit_is_validated_and_applied()
    {
        var admin = await api.ClientAsAsync("admin");
        (await admin.PostAsync("/api/equipment/MX01/maintenance/start", null, Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsync("/api/equipment/MX01/maintenance/end", null, Ct)).EnsureSuccessStatusCode();

        var one = await admin.GetFromJsonAsync<List<EquipmentStatusLogDto>>(
            "/api/equipment/MX01/status-log?limit=1", ProductionDriver.JsonOptions, Ct);

        Assert.Equal("Maintenance ended", Assert.Single(one!).Reason);
    }
}
