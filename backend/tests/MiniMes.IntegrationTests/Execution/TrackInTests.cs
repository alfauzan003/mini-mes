using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.IntegrationTests.Execution;

[Collection("api")]
public class TrackInTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly string[] CathodeMix = ["NCM811", "PVDF", "SUPER-P", "NMP"];

    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Mix_track_in_starts_run_and_sets_states()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync(CathodeMix);

        var run = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, lots);

        Assert.Equal("MX01", run.EquipmentCode);
        Assert.Equal(wo.Number, run.WorkOrderNumber);
        Assert.Equal(OperationCode.Mix, run.Operation);
        Assert.Equal("operator", run.Operator);
        Assert.Null(run.EndedAt);
        Assert.Null(run.ParentLotId);
        Assert.Empty(run.Outputs);
        Assert.Equal(lots.Order(), run.Inputs.Select(i => i.LotId).Order());
        Assert.All(run.Inputs, i =>
        {
            Assert.Equal(RunInputRole.Secondary, i.Role);
            Assert.Equal(LotType.Raw, i.Type);
            Assert.Equal(500m, i.Qty);
            Assert.Equal("kg", i.Uom);
            Assert.Null(i.ConsumedQty);
        });

        var equipment = await _driver.EquipmentAsync("MX01");
        Assert.Equal(EquipmentStatus.Running, equipment.Status);
        Assert.Equal(run.Id, equipment.OpenRun?.Id);
        Assert.Equal(WorkOrderStatus.Running, (await _driver.WorkOrderAsync(wo)).Status);
        foreach (var lotId in lots)
        {
            var lot = await _driver.LotAsync(lotId);
            Assert.Equal(LotStatus.Run, lot.Status);
            Assert.Equal("MX01", lot.CurrentEquipment);
        }
    }

    [Fact]
    public async Task Track_in_writes_a_track_in_event_per_lot()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811", "PVDF");

        var run = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, lots);

        var client = await _driver.OperatorAsync();
        foreach (var lotId in lots)
        {
            var events = await client.GetFromJsonAsync<JsonElement>($"/api/lots/{lotId}/events", Ct);
            var trackIn = events.EnumerateArray().Last();
            Assert.Equal("TRACK_IN", trackIn.GetProperty("type").GetString());
            Assert.Equal("operator", trackIn.GetProperty("user").GetString());
            Assert.Equal("MX01", trackIn.GetProperty("equipment").GetString());
            Assert.Equal(run.Id, trackIn.GetProperty("runId").GetGuid());
            Assert.Equal(500m, trackIn.GetProperty("qty").GetDecimal());
        }
    }

    [Fact]
    public async Task Run_is_readable_by_id_and_listed_as_open_for_its_equipment()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811");
        var run = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, lots);
        var client = await _driver.OperatorAsync();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/runs/{run.Id}", Ct);
        var open = await client.GetFromJsonAsync<JsonElement>("/api/runs?equipment=mx01&open=true", Ct);
        var otherEquipment = await client.GetFromJsonAsync<JsonElement>("/api/runs?equipment=MX02&open=true", Ct);
        var closed = await client.GetFromJsonAsync<JsonElement>("/api/runs?open=false", Ct);
        var missing = await client.GetAsync($"/api/runs/{Guid.NewGuid()}", Ct);

        Assert.Equal(run.Id, fetched.GetProperty("id").GetGuid());
        Assert.Equal("MIX", fetched.GetProperty("operation").GetString());
        Assert.Equal(run.Id, Assert.Single(open.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(0, otherEquipment.GetArrayLength());
        Assert.Equal(0, closed.GetArrayLength());
        await ProductionDriver.AssertErrorAsync(missing, 404, "RUN_NOT_FOUND");
    }

    [Fact]
    public async Task Unassigned_equipment_is_equipment_not_assigned()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811");

        var response = await _driver.TrackInAsync("MX02", wo, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 422, "EQUIPMENT_NOT_ASSIGNED");
    }

    [Fact]
    public async Task Unknown_equipment_is_404_equipment_not_found()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811");

        var response = await _driver.TrackInAsync("MX99", wo, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 404, "EQUIPMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Unknown_work_order_operation_is_404_wo_not_found()
    {
        var lots = await _driver.MaterialLotsAsync("NCM811");
        var client = await _driver.OperatorAsync();

        var response = await client.PostAsJsonAsync(
            "/api/runs/track-in",
            new { equipmentCode = "MX01", workOrderOperationId = Guid.NewGuid(), inputs = lots }, Ct);

        await ProductionDriver.AssertErrorAsync(response, 404, "WO_NOT_FOUND");
    }

    [Fact]
    public async Task Planned_order_is_wo_not_active()
    {
        var wo = await _driver.CreateWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811");

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 422, "WO_NOT_ACTIVE");
    }

    [Fact]
    public async Task Held_order_is_wo_not_active()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        await _driver.ChangeStatusAsync(wo, "hold");
        var lots = await _driver.MaterialLotsAsync("NCM811");

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 422, "WO_NOT_ACTIVE");
    }

    [Fact]
    public async Task Second_track_in_on_running_equipment_is_not_available()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync("NCM811"));
        var more = await _driver.MaterialLotsAsync("PVDF");

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, more);

        await ProductionDriver.AssertErrorAsync(response, 422, "EQUIPMENT_NOT_AVAILABLE");
        Assert.Equal(LotStatus.Wait, (await _driver.LotAsync(more[0])).Status);
    }

    [Fact]
    public async Task Lot_already_running_is_lot_not_available()
    {
        var first = await _driver.CreateReleasedWorkOrderAsync(mix: "MX01");
        var second = await _driver.CreateReleasedWorkOrderAsync(mix: "MX02");
        var lots = await _driver.MaterialLotsAsync("NCM811");
        await _driver.TrackInOkAsync("MX01", first, OperationCode.Mix, lots);

        var response = await _driver.TrackInAsync("MX02", second, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 422, "LOT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Anode_raw_into_cathode_order_is_polarity_mismatch_and_writes_nothing()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811", "PVDF", "GRAPHITE");

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 422, "POLARITY_MISMATCH");
        Assert.Equal(EquipmentStatus.Idle, (await _driver.EquipmentAsync("MX01")).Status);
        Assert.Equal(WorkOrderStatus.Released, (await _driver.WorkOrderAsync(wo)).Status);
        foreach (var lotId in lots)
        {
            Assert.Equal(LotStatus.Wait, (await _driver.LotAsync(lotId)).Status);
        }
    }

    [Fact]
    public async Task Foil_into_mix_is_invalid_input_set()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("AL-FOIL");

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, lots);

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_INPUT_SET");
    }

    [Fact]
    public async Task Empty_or_duplicate_scans_are_invalid_input_set()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lot = (await _driver.MaterialLotsAsync("NCM811"))[0];

        await ProductionDriver.AssertErrorAsync(
            await _driver.TrackInAsync("MX01", wo, OperationCode.Mix), 422, "INVALID_INPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, lot, lot.ToLowerInvariant()),
            422, "INVALID_INPUT_SET");
    }

    [Fact]
    public async Task Scan_is_trimmed_and_case_insensitive()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lotId = (await _driver.MaterialLotsAsync("NCM811"))[0];

        var response = await _driver.TrackInAsync(
            " mx01 ", wo, OperationCode.Mix, "  " + lotId.ToLowerInvariant() + " ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(LotStatus.Run, (await _driver.LotAsync(lotId)).Status);
    }

    [Fact]
    public async Task Unknown_scan_is_404_scan_not_resolved()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, "RC-000000-999");

        await ProductionDriver.AssertErrorAsync(response, 404, "SCAN_NOT_RESOLVED");
    }

    [Fact]
    public async Task Empty_carrier_scan_is_404_scan_not_resolved()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, "  bb-0001 ");

        await ProductionDriver.AssertErrorAsync(response, 404, "SCAN_NOT_RESOLVED");
    }

    [Fact]
    public async Task Planner_cannot_track_in()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811");
        var planner = await _driver.PlannerAsync();

        var response = await planner.PostAsJsonAsync(
            "/api/runs/track-in",
            new { equipmentCode = "MX01", workOrderOperationId = wo.Operations[0].Id, inputs = lots }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
