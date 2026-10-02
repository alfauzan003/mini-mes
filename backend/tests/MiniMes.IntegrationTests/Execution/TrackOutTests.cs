using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Features.TrackOut;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;
using MiniMes.Api.Shared.Quality;

namespace MiniMes.IntegrationTests.Execution;

[Collection("api")]
public class TrackOutTests(MesApiFactory api) : IAsyncLifetime
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
    public async Task Mix_track_out_ends_run_and_returns_partly_used_lots_to_wait()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync(CathodeMix);
        var run = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, lots);
        await _driver.ProduceOkAsync(run.Id, new OutputLine(null, null, 480m, 20m));

        var ended = await _driver.TrackOutOkAsync(
            run.Id, new Consumption(lots[0], 100m), new Consumption("  " + lots[1].ToLowerInvariant(), 150.5m));

        Assert.NotNull(ended.EndedAt);
        Assert.Equal(480m, ended.GoodQty);
        Assert.Equal(20m, ended.RejectQty);
        Assert.Equal(
            [100m, 150.5m, 500m, 500m],
            ended.Inputs.OrderBy(i => Array.IndexOf(lots, i.LotId)).Select(i => i.ConsumedQty!.Value));

        var first = await _driver.LotAsync(lots[0]);
        Assert.Equal(LotStatus.Wait, first.Status);
        Assert.Equal(400m, first.Qty);
        var second = await _driver.LotAsync(lots[1]);
        Assert.Equal(LotStatus.Wait, second.Status);
        Assert.Equal(349.5m, second.Qty);
        foreach (var unlisted in lots.Skip(2))
        {
            var lot = await _driver.LotAsync(unlisted);
            Assert.Equal(LotStatus.Consumed, lot.Status);
            Assert.Equal(0m, lot.Qty);
        }

        var equipment = await _driver.EquipmentAsync("MX01");
        Assert.Equal(EquipmentStatus.Idle, equipment.Status);
        Assert.Null(equipment.OpenRun);
    }

    [Fact]
    public async Task Track_out_writes_a_track_out_event_for_the_run_operation()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811");
        var run = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, lots);
        await _driver.ProduceOkAsync(run.Id, new OutputLine(null, null, 480m, 20m));

        await _driver.TrackOutOkAsync(run.Id, new Consumption(lots[0], 100m));

        var events = await (await _driver.OperatorAsync()).GetFromJsonAsync<JsonElement>($"/api/lots/{lots[0]}/events", Ct);
        var trackOut = events.EnumerateArray().Last();
        Assert.Equal("TRACK_OUT", trackOut.GetProperty("type").GetString());
        Assert.Equal("MIX", trackOut.GetProperty("operation").GetString());
        Assert.Equal("MX01", trackOut.GetProperty("equipment").GetString());
        Assert.Equal(run.Id, trackOut.GetProperty("runId").GetGuid());
        Assert.Equal("operator", trackOut.GetProperty("user").GetString());
        Assert.Equal(100m, trackOut.GetProperty("qty").GetDecimal());
    }

    [Fact]
    public async Task Unknown_run_is_404_run_not_found()
    {
        var response = await _driver.TrackOutAsync(Guid.NewGuid());

        await ProductionDriver.AssertErrorAsync(response, 404, "RUN_NOT_FOUND");
    }

    [Fact]
    public async Task Track_out_twice_is_run_not_open()
    {
        var (_, run, lots) = await StartMixAsync();
        await _driver.TrackOutOkAsync(run.Id, new Consumption(lots[0], 100m));

        var again = await _driver.TrackOutAsync(run.Id, new Consumption(lots[0], 100m));

        await ProductionDriver.AssertErrorAsync(again, 422, "RUN_NOT_OPEN");
        Assert.Equal(400m, (await _driver.LotAsync(lots[0])).Qty);
    }

    [Fact]
    public async Task Consumption_over_qty_is_qty_exceeds_lot_and_run_stays_open()
    {
        var (_, run, lots) = await StartMixAsync();

        var response = await _driver.TrackOutAsync(
            run.Id, new Consumption(lots[0], 100m), new Consumption(lots[1], 500.001m));

        await ProductionDriver.AssertErrorAsync(response, 422, "QTY_EXCEEDS_LOT");
        await AssertNothingChangedAsync(run, lots);
    }

    [Fact]
    public async Task Negative_consumption_is_invalid_quantity()
    {
        var (_, run, lots) = await StartMixAsync();

        var response = await _driver.TrackOutAsync(
            run.Id, new Consumption(lots[0], 100m), new Consumption(lots[1], -1m));

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_QUANTITY");
        await AssertNothingChangedAsync(run, lots);
    }

    [Fact]
    public async Task Consumption_with_more_than_three_decimals_is_invalid_quantity()
    {
        var (_, run, lots) = await StartMixAsync();

        var response = await _driver.TrackOutAsync(run.Id, new Consumption(lots[0], 100.0005m));

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_QUANTITY");
        await AssertNothingChangedAsync(run, lots);
    }

    [Fact]
    public async Task Consuming_lot_not_in_run_is_invalid_input_set()
    {
        var (_, run, lots) = await StartMixAsync();
        var outsider = (await _driver.MaterialLotsAsync("NCM811")).Single();

        var response = await _driver.TrackOutAsync(
            run.Id, new Consumption(lots[0], 100m), new Consumption(outsider, 10m));
        var unknown = await _driver.TrackOutAsync(run.Id, new Consumption("NO-SUCH-LOT", 10m));

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_INPUT_SET");
        await ProductionDriver.AssertErrorAsync(unknown, 422, "INVALID_INPUT_SET");
        await AssertNothingChangedAsync(run, lots);
        Assert.Equal(LotStatus.Wait, (await _driver.LotAsync(outsider)).Status);
    }

    [Fact]
    public async Task Listing_a_lot_twice_is_invalid_input_set()
    {
        var (_, run, lots) = await StartMixAsync();

        var response = await _driver.TrackOutAsync(
            run.Id, new Consumption(lots[0], 100m), new Consumption(lots[0].ToLowerInvariant(), 50m));

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_INPUT_SET");
        await AssertNothingChangedAsync(run, lots);
    }

    [Fact]
    public async Task Remaining_foil_can_be_used_by_a_later_run()
    {
        var (wo, foil, slurry) = await CoatStartedAsync();
        var first = await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        await _driver.ProduceOkAsync(first.Id, new OutputLine("BB-0001", null, 1200m, 20m));

        await _driver.TrackOutOkAsync(first.Id, new Consumption(foil, 1300m), new Consumption(slurry, 200m));

        var foilLot = await _driver.LotAsync(foil);
        Assert.Equal(LotStatus.Wait, foilLot.Status);
        Assert.Equal(4700m, foilLot.Qty);
        var slurryLot = await _driver.LotAsync(slurry);
        Assert.Equal(LotStatus.Wait, slurryLot.Status);
        Assert.Equal(280m, slurryLot.Qty);

        var second = await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        await _driver.ProduceOkAsync(second.Id, new OutputLine("BB-0002", null, 1200m, 20m));
        await _driver.TrackOutOkAsync(second.Id, new Consumption(foil, 1300m));

        Assert.Equal(3400m, (await _driver.LotAsync(foil)).Qty);
        Assert.Equal(LotStatus.Consumed, (await _driver.LotAsync(slurry)).Status);
        var coat = (await _driver.WorkOrderAsync(wo)).Operations.Single(o => o.Operation == OperationCode.Coat);
        Assert.Equal(2, coat.RunCount);
        Assert.Equal(2400m, coat.OutputQty);
    }

    [Fact]
    public async Task Calendering_track_out_leaves_the_calendered_roll_waiting_on_its_new_bobbin()
    {
        var (wo, foil, slurry) = await CoatStartedAsync();
        var coat = await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        var roll = (await _driver.ProduceOkAsync(coat.Id, new OutputLine("BB-0001", null, 1200m, 20m)))
            .Outputs.Single().LotId!;
        await _driver.TrackOutOkAsync(coat.Id);
        var cal = await _driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
        await _driver.ProduceOkAsync(cal.Id, new OutputLine("BB-0002", null, 1180m, 20m));

        var ended = await _driver.TrackOutOkAsync(cal.Id);

        Assert.NotNull(ended.EndedAt);
        Assert.Equal(1180m, ended.GoodQty);
        Assert.Null(Assert.Single(ended.Inputs).ConsumedQty);
        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(1180m, lot.Qty);
        Assert.Equal("BB-0002", lot.CurrentCarrier);
        Assert.Equal(CarrierStatus.Full, (await _driver.CarrierAsync("BB-0002")).Status);
        Assert.Equal(EquipmentStatus.Idle, (await _driver.EquipmentAsync("CP01")).Status);
        var events = await (await _driver.OperatorAsync()).GetFromJsonAsync<JsonElement>($"/api/lots/{roll}/events", Ct);
        Assert.DoesNotContain(
            events.EnumerateArray(),
            e => e.GetProperty("type").GetString() == "TRACK_OUT" && e.GetProperty("runId").GetGuid() == cal.Id);
    }

    [Fact]
    public async Task Calendering_track_out_leaves_a_roll_already_running_on_the_slitter_alone()
    {
        var (wo, _, cal, roll) = await CalenderedStartedAsync();
        await _driver.ProduceOkAsync(cal.Id, new OutputLine("BB-0002", null, 1180m, 20m));
        var slit = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0002");

        var ended = await _driver.TrackOutOkAsync(cal.Id);

        Assert.NotNull(ended.EndedAt);
        Assert.Null(Assert.Single(ended.Inputs).ConsumedQty);
        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Run, lot.Status);
        Assert.Equal(1180m, lot.Qty);
        Assert.Equal("BB-0002", lot.CurrentCarrier);
        Assert.Equal(CarrierStatus.Full, (await _driver.CarrierAsync("BB-0002")).Status);
        Assert.Equal(EquipmentStatus.Idle, (await _driver.EquipmentAsync("CP01")).Status);
        Assert.Equal(EquipmentStatus.Running, (await _driver.EquipmentAsync("SL01")).Status);

        await _driver.ProduceOkAsync(slit.Id, Grid());
        var slitEnded = await _driver.TrackOutOkAsync(slit.Id);
        Assert.Equal(1180m, Assert.Single(slitEnded.Inputs).ConsumedQty);
        Assert.Equal(LotStatus.Consumed, (await _driver.LotAsync(roll)).Status);
        Assert.Equal(CarrierStatus.Empty, (await _driver.CarrierAsync("BB-0002")).Status);
    }

    [Fact]
    public async Task Calendering_track_out_without_output_returns_the_roll_to_wait_unchanged()
    {
        var (_, _, cal, roll) = await CalenderedStartedAsync();

        var ended = await _driver.TrackOutOkAsync(cal.Id);

        Assert.NotNull(ended.EndedAt);
        Assert.Equal(0m, Assert.Single(ended.Inputs).ConsumedQty);
        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(1200m, lot.Qty);
        Assert.Equal("BB-0001", lot.CurrentCarrier);
        Assert.Equal(OperationCode.Cal, lot.NextOperation);
        Assert.Equal(CarrierStatus.Full, (await _driver.CarrierAsync("BB-0001")).Status);
        Assert.Equal(EquipmentStatus.Idle, (await _driver.EquipmentAsync("CP01")).Status);
        var events = await (await _driver.OperatorAsync()).GetFromJsonAsync<JsonElement>($"/api/lots/{roll}/events", Ct);
        var last = events.EnumerateArray().Last();
        Assert.Equal("TRACK_OUT", last.GetProperty("type").GetString());
        Assert.Equal("CAL", last.GetProperty("operation").GetString());
        Assert.Equal(0m, last.GetProperty("qty").GetDecimal());
    }

    [Fact]
    public async Task Listing_the_calendered_roll_in_consumptions_is_invalid_input_set()
    {
        var (_, _, cal, roll) = await CalenderedStartedAsync();

        var response = await _driver.TrackOutAsync(cal.Id, new Consumption(roll, 1200m));

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_INPUT_SET");
        var fetched = await _driver.RunAsync(cal.Id);
        Assert.Null(fetched.EndedAt);
        Assert.Null(Assert.Single(fetched.Inputs).ConsumedQty);
        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Run, lot.Status);
        Assert.Equal(1200m, lot.Qty);
        Assert.Equal(EquipmentStatus.Running, (await _driver.EquipmentAsync("CP01")).Status);
    }

    [Fact]
    public async Task Slitting_must_consume_the_whole_electrode()
    {
        var (wo, _, cal, roll) = await CalenderedStartedAsync();
        await _driver.ProduceOkAsync(cal.Id, new OutputLine("BB-0002", null, 1180m, 20m));
        await _driver.TrackOutOkAsync(cal.Id);
        var slit = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0002");
        await _driver.ProduceOkAsync(slit.Id, Grid());

        var partial = await _driver.TrackOutAsync(slit.Id, new Consumption(roll, 1000m));
        var excess = await _driver.TrackOutAsync(slit.Id, new Consumption(roll, 1180.001m));

        await ProductionDriver.AssertErrorAsync(partial, 422, "INVALID_QUANTITY");
        await ProductionDriver.AssertErrorAsync(excess, 422, "INVALID_QUANTITY");
        var fetched = await _driver.RunAsync(slit.Id);
        Assert.Null(fetched.EndedAt);
        Assert.Null(Assert.Single(fetched.Inputs).ConsumedQty);
        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Run, lot.Status);
        Assert.Equal(1180m, lot.Qty);
        Assert.Equal(EquipmentStatus.Running, (await _driver.EquipmentAsync("SL01")).Status);

        var full = await _driver.TrackOutOkAsync(slit.Id, new Consumption(roll, 1180m));

        Assert.Equal(1180m, Assert.Single(full.Inputs).ConsumedQty);
        Assert.Equal(LotStatus.Consumed, (await _driver.LotAsync(roll)).Status);
    }

    [Fact]
    public async Task Slitting_track_out_without_output_is_invalid_output_set_until_produced()
    {
        var (wo, _, cal, roll) = await CalenderedStartedAsync();
        await _driver.ProduceOkAsync(cal.Id, new OutputLine("BB-0002", null, 1180m, 20m));
        await _driver.TrackOutOkAsync(cal.Id);
        var slit = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0002");

        var response = await _driver.TrackOutAsync(slit.Id);

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_OUTPUT_SET");
        var fetched = await _driver.RunAsync(slit.Id);
        Assert.Null(fetched.EndedAt);
        Assert.Null(Assert.Single(fetched.Inputs).ConsumedQty);
        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Run, lot.Status);
        Assert.Equal(1180m, lot.Qty);
        Assert.Equal(EquipmentStatus.Running, (await _driver.EquipmentAsync("SL01")).Status);

        await _driver.ProduceOkAsync(slit.Id, Grid());
        var ended = await _driver.TrackOutOkAsync(slit.Id);

        Assert.NotNull(ended.EndedAt);
        Assert.Equal(LotStatus.Consumed, (await _driver.LotAsync(roll)).Status);
    }

    [Fact]
    public async Task Null_consumption_element_or_omitted_quantity_is_a_400_bad_request()
    {
        var (_, run, lots) = await StartMixAsync();
        var client = await _driver.OperatorAsync();

        var nullElement = await client.PostAsJsonAsync(
            $"/api/runs/{run.Id}/track-out", new { consumptions = new object?[] { null } }, Ct);
        var noQuantity = await client.PostAsJsonAsync(
            $"/api/runs/{run.Id}/track-out", new { consumptions = new[] { new { lotId = lots[0] } } }, Ct);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, nullElement.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, noQuantity.StatusCode);
        await AssertNothingChangedAsync(run, lots);
    }

    /// <summary>A released order whose roll sits on BB-0001 and is running in a calendering run on CP01.</summary>
    private async Task<(WorkOrderDto Wo, RunDto Coat, RunDto Cal, string Roll)> CalenderedStartedAsync()
    {
        var (wo, foil, slurry) = await CoatStartedAsync();
        var coat = await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        var roll = (await _driver.ProduceOkAsync(coat.Id, new OutputLine("BB-0001", null, 1200m, 20m)))
            .Outputs.Single().LotId!;
        await _driver.TrackOutOkAsync(coat.Id);
        var cal = await _driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
        return (wo, coat, cal, roll);
    }

    private static OutputLine[] Grid() =>
        [.. Enumerable.Range(1, 8).Select(lane => new OutputLine($"PC-{lane:0000}", lane, 145m, 2m))];

    [Fact]
    public async Task Using_up_a_roll_unloads_its_carrier_and_records_which_one()
    {
        var flow = await _driver.RunFullFlowAsync(target: 8);
        var (roll, slitRunId) = (flow.Electrode, flow.SlitRunId);

        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Consumed, lot.Status);
        Assert.Null(lot.CurrentCarrier);
        var carrier = await _driver.CarrierAsync("BB-0002");
        Assert.Equal(CarrierStatus.Empty, carrier.Status);
        Assert.Null(carrier.LotId);

        var events = await (await _driver.OperatorAsync()).GetFromJsonAsync<JsonElement>($"/api/lots/{roll}/events", Ct);
        var tail = events.EnumerateArray().TakeLast(2).ToArray();
        Assert.Equal(["TRACK_OUT", "CARRIER_UNLOAD"], tail.Select(e => e.GetProperty("type").GetString()));
        Assert.Equal("SLIT", tail[0].GetProperty("operation").GetString());
        Assert.Equal("BB-0002", tail[1].GetProperty("carrier").GetString());
        Assert.Equal(slitRunId, tail[1].GetProperty("runId").GetGuid());
        Assert.Equal(1180m, tail[0].GetProperty("qty").GetDecimal());
    }

    [Fact]
    public async Task Unfinished_inspection_blocks_next_step()
    {
        await using var inspecting = api.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(
                services => services.AddScoped<IInspectionRequirement, RequiresCoatInspection>()));
        var driver = new ProductionDriver(inspecting);
        var wo = await driver.CreateReleasedWorkOrderAsync();
        var mix = await driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, await driver.MaterialLotsAsync(CathodeMix));
        var slurry = (await driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m))).Outputs.Single().LotId!;
        await driver.TrackOutOkAsync(mix.Id);
        var foil = (await driver.MaterialLotsAsync("AL-FOIL")).Single();
        var coat = await driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        await driver.ProduceOkAsync(coat.Id, new OutputLine("BB-0001", null, 1200m, 20m));
        await driver.TrackOutOkAsync(coat.Id);

        var response = await driver.TrackInAsync("CP01", wo, OperationCode.Cal, "BB-0001");

        await ProductionDriver.AssertErrorAsync(response, 422, "LOT_QUALITY_PENDING");
        Assert.Equal(EquipmentStatus.Idle, (await driver.EquipmentAsync("CP01")).Status);
    }

    private sealed class RequiresCoatInspection : IInspectionRequirement
    {
        public Task<bool> IsRequiredAsync(Guid productId, OperationCode operation, CancellationToken ct) =>
            Task.FromResult(operation == OperationCode.Coat);
    }

    private async Task<(WorkOrderDto Wo, RunDto Run, string[] Lots)> StartMixAsync()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var lots = await _driver.MaterialLotsAsync("NCM811", "PVDF");
        var run = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, lots);
        return (wo, run, lots);
    }

    /// <summary>A released order with a finished mixing run: one foil lot and one slurry lot ready for COAT.</summary>
    private async Task<(WorkOrderDto Wo, string Foil, string Slurry)> CoatStartedAsync()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var mix = await _driver.TrackInOkAsync(
            "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));
        var slurry = (await _driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m)))
            .Outputs.Single().LotId!;
        await _driver.TrackOutOkAsync(mix.Id);
        return (wo, (await _driver.MaterialLotsAsync("AL-FOIL")).Single(), slurry);
    }

    /// <summary>A rejected track-out changes nothing: the run is open and every input is still running.</summary>
    private async Task AssertNothingChangedAsync(RunDto run, string[] lots)
    {
        var fetched = await _driver.RunAsync(run.Id);
        Assert.Null(fetched.EndedAt);
        Assert.All(fetched.Inputs, i => Assert.Null(i.ConsumedQty));
        foreach (var lotId in lots)
        {
            var lot = await _driver.LotAsync(lotId);
            Assert.Equal(LotStatus.Run, lot.Status);
            Assert.Equal(500m, lot.Qty);
        }

        var equipment = await _driver.EquipmentAsync(run.EquipmentCode);
        Assert.Equal(EquipmentStatus.Running, equipment.Status);
        Assert.Equal(run.Id, equipment.OpenRun?.Id);
    }
}
