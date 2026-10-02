using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;
using MiniMes.Api.Shared.Data;

namespace MiniMes.IntegrationTests.Execution;

[Collection("api")]
public class ProduceOutputTests(MesApiFactory api) : IAsyncLifetime
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

    private static OutputLine OnBobbin(string carrier, decimal good = 1200m, decimal reject = 20m) =>
        new(carrier, null, good, reject);

    [Fact]
    public async Task Mix_produces_one_slurry_lot()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var run = await _driver.TrackInOkAsync(
            "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));

        var produced = await _driver.ProduceOkAsync(run.Id, new OutputLine(null, null, 480m, 20m));

        var output = Assert.Single(produced.Outputs);
        Assert.Matches(@"^SC-\d{6}-MX01-01$", output.LotId);
        Assert.Null(output.CarrierCode);
        Assert.Null(output.Lane);
        Assert.Equal(480m, output.GoodQty);
        Assert.Equal(20m, output.RejectQty);
        Assert.Null(produced.EndedAt);

        var slurry = await _driver.LotAsync(output.LotId!);
        Assert.Equal(LotType.Slurry, slurry.Type);
        Assert.Equal("kg", slurry.Uom);
        Assert.Equal(480m, slurry.Qty);
        Assert.Equal(LotStatus.Wait, slurry.Status);
        Assert.Equal(QualityStatus.None, slurry.Quality);
        Assert.Equal(OperationCode.Mix, slurry.CurrentOperation);
        Assert.Equal(OperationCode.Coat, slurry.NextOperation);
        Assert.Equal(wo.Number, slurry.WorkOrderNumber);
        Assert.Null(slurry.CurrentCarrier);

        var client = await _driver.OperatorAsync();
        var listed = await client.GetFromJsonAsync<JsonElement>($"/api/lots?workOrder={wo.Number}", Ct);
        Assert.Equal(output.LotId, Assert.Single(listed.EnumerateArray()).GetProperty("lotId").GetString());

        var events = await client.GetFromJsonAsync<JsonElement>($"/api/lots/{output.LotId}/events", Ct);
        var created = Assert.Single(events.EnumerateArray());
        Assert.Equal("CREATE", created.GetProperty("type").GetString());
        Assert.Equal(run.Id, created.GetProperty("runId").GetGuid());
        Assert.Equal(480m, created.GetProperty("qty").GetDecimal());

        Assert.Equal(run.Inputs.Select(i => i.LotId).Order(), (await ParentsOfAsync(output.LotId!)).Order());
    }

    [Fact]
    public async Task Mix_second_produce_is_invalid_output_set()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var run = await _driver.TrackInOkAsync(
            "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));
        await _driver.ProduceOkAsync(run.Id, new OutputLine(null, null, 480m, 20m));

        var again = await _driver.ProduceAsync(run.Id, new OutputLine(null, null, 480m, 20m));

        await ProductionDriver.AssertErrorAsync(again, 422, "INVALID_OUTPUT_SET");
        Assert.Equal(1, await CountLotsAsync("type=SLURRY"));
    }

    [Fact]
    public async Task Mix_rejects_a_carrier_a_lane_zero_good_or_several_lines()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var run = await _driver.TrackInOkAsync(
            "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));

        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine("BB-0001", null, 480m, 0m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine(null, 1, 480m, 0m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine(null, null, 0m, 20m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine(null, null, 1m, 0m), new OutputLine(null, null, 1m, 0m)),
            422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(await _driver.ProduceAsync(run.Id), 422, "INVALID_OUTPUT_SET");
        Assert.Equal(0, await CountLotsAsync("type=SLURRY"));
    }

    [Fact]
    public async Task Concurrent_double_submit_on_mix_creates_exactly_one_slurry_lot()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await api.ResetDatabaseAsync();
            _driver = new ProductionDriver(api);
            var wo = await _driver.CreateReleasedWorkOrderAsync();
            var run = await _driver.TrackInOkAsync(
                "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));

            var responses = await Task.WhenAll(
                _driver.ProduceAsync(run.Id, new OutputLine(null, null, 480m, 20m)),
                _driver.ProduceAsync(run.Id, new OutputLine(null, null, 480m, 20m)));

            Assert.Single(responses, r => r.StatusCode == System.Net.HttpStatusCode.OK);
            await ProductionDriver.AssertErrorAsync(
                responses.Single(r => r.StatusCode != System.Net.HttpStatusCode.OK), 422, "INVALID_OUTPUT_SET");
            Assert.Equal(1, await CountLotsAsync("type=SLURRY"));
        }
    }

    [Fact]
    public async Task Concurrent_double_submit_on_slit_with_different_cores_is_one_grid()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await api.ResetDatabaseAsync();
            _driver = new ProductionDriver(api);
            var (wo, _) = await CalenderedRollAsync();
            var run = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0004");
            var second = Grid().Select(l => l with { CarrierCode = l.CarrierCode!.Replace("PC-00", "PC-01") }).ToArray();

            var responses = await Task.WhenAll(_driver.ProduceAsync(run.Id, Grid()), _driver.ProduceAsync(run.Id, second));

            Assert.Single(responses, r => r.StatusCode == System.Net.HttpStatusCode.OK);
            await ProductionDriver.AssertErrorAsync(
                responses.Single(r => r.StatusCode != System.Net.HttpStatusCode.OK), 422, "INVALID_OUTPUT_SET");
            Assert.Equal(8, await CountLotsAsync("type=PANCAKE"));
        }
    }

    [Fact]
    public async Task Negative_reject_is_invalid_quantity()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var run = await _driver.TrackInOkAsync(
            "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));

        var negativeReject = await _driver.ProduceAsync(run.Id, new OutputLine(null, null, 480m, -5m));
        var negativeGood = await _driver.ProduceAsync(run.Id, new OutputLine(null, null, -1m, 20m));
        var empty = await _driver.ProduceAsync(run.Id, new OutputLine(null, null, 0m, 0m));

        await ProductionDriver.AssertErrorAsync(negativeReject, 422, "INVALID_QUANTITY");
        await ProductionDriver.AssertErrorAsync(negativeGood, 422, "INVALID_QUANTITY");
        await ProductionDriver.AssertErrorAsync(empty, 422, "INVALID_QUANTITY");
        Assert.Equal(0, await CountLotsAsync("type=SLURRY"));
        var fetched = await (await _driver.OperatorAsync()).GetFromJsonAsync<JsonElement>($"/api/runs/{run.Id}", Ct);
        Assert.Equal(0, fetched.GetProperty("outputs").GetArrayLength());
    }

    [Fact]
    public async Task Quantity_with_more_than_three_decimals_or_beyond_the_column_is_invalid_quantity()
    {
        var (wo, _) = await CalenderedRollAsync();
        var run = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0004");
        var tinyGood = Grid();
        tinyGood[0] = tinyGood[0] with { GoodQty = 0.0004m };
        var preciseReject = Grid();
        preciseReject[1] = preciseReject[1] with { RejectQty = 1.0005m };
        var tooLarge = Grid();
        tooLarge[2] = tooLarge[2] with { GoodQty = 1_000_000_000m };

        var responses = new[]
        {
            await _driver.ProduceAsync(run.Id, tinyGood),
            await _driver.ProduceAsync(run.Id, preciseReject),
            await _driver.ProduceAsync(run.Id, tooLarge)
        };

        foreach (var response in responses)
        {
            await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_QUANTITY");
        }

        Assert.Equal(0, await CountLotsAsync("type=PANCAKE"));
        Assert.Equal(0, (await _driver.WorkOrderAsync(wo)).GoodCount);
    }

    [Fact]
    public async Task Null_output_line_is_a_400_bad_request()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var run = await _driver.TrackInOkAsync(
            "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));

        var response = await (await _driver.OperatorAsync())
            .PostAsJsonAsync($"/api/runs/{run.Id}/outputs", new { outputs = new object?[] { null } }, Ct);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountLotsAsync("type=SLURRY"));
    }

    [Fact]
    public async Task Unknown_run_is_404_run_not_found()
    {
        var response = await _driver.ProduceAsync(Guid.NewGuid(), new OutputLine(null, null, 1m, 0m));

        await ProductionDriver.AssertErrorAsync(response, 404, "RUN_NOT_FOUND");
    }

    [Fact]
    public async Task Coat_produces_rolls_on_bobbins()
    {
        var (wo, slurry) = await MixAsync();
        var foil = (await _driver.MaterialLotsAsync("AL-FOIL")).Single();
        var run = await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);

        var produced = await _driver.ProduceOkAsync(
            run.Id, OnBobbin("BB-0001"), OnBobbin("BB-0002", 1100m, 40m), OnBobbin("BB-0003"));

        Assert.Equal(3, produced.Outputs.Count);
        var lotIds = produced.Outputs.Select(o => o.LotId!).ToList();
        Assert.Equal(1100m, produced.Outputs[1].GoodQty);
        Assert.Equal(40m, produced.Outputs[1].RejectQty);
        for (var i = 0; i < 3; i++)
        {
            Assert.Matches($@"^EC-\d{{6}}-CT01-00{i + 1}$", lotIds[i]);
            Assert.Equal($"BB-000{i + 1}", produced.Outputs[i].CarrierCode);

            var carrier = await _driver.CarrierAsync($"BB-000{i + 1}");
            Assert.Equal(CarrierStatus.Full, carrier.Status);
            Assert.Equal(lotIds[i], carrier.LotId);

            var lot = await _driver.LotAsync(lotIds[i]);
            Assert.Equal(LotType.Electrode, lot.Type);
            Assert.Equal("m", lot.Uom);
            Assert.Equal(LotStatus.Wait, lot.Status);
            Assert.Equal(QualityStatus.None, lot.Quality);
            Assert.Equal(OperationCode.Coat, lot.CurrentOperation);
            Assert.Equal(OperationCode.Cal, lot.NextOperation);
            Assert.Equal($"BB-000{i + 1}", lot.CurrentCarrier);
            Assert.Equal(wo.Number, lot.WorkOrderNumber);
            Assert.Equal(new[] { foil, slurry }.Order(), (await ParentsOfAsync(lotIds[i])).Order());
        }

        var events = await (await _driver.OperatorAsync())
            .GetFromJsonAsync<JsonElement>($"/api/lots/{lotIds[0]}/events", Ct);
        Assert.Equal(
            ["CREATE", "CARRIER_LOAD"],
            events.EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToArray());
        Assert.Equal("BB-0001", events.EnumerateArray().Last().GetProperty("carrier").GetString());
    }

    [Fact]
    public async Task Coat_carrier_code_is_normalized_and_unknown_carrier_is_404()
    {
        var (_, run) = await StartCoatAsync();

        var unknown = await _driver.ProduceAsync(run.Id, OnBobbin("BB-9999"));
        var produced = await _driver.ProduceOkAsync(run.Id, OnBobbin("  bb-0001 "));

        await ProductionDriver.AssertErrorAsync(unknown, 404, "CARRIER_NOT_FOUND");
        Assert.Equal("BB-0001", Assert.Single(produced.Outputs).CarrierCode);
    }

    [Fact]
    public async Task Coat_on_pancake_core_is_carrier_type_mismatch()
    {
        var (_, run) = await StartCoatAsync();

        var response = await _driver.ProduceAsync(run.Id, OnBobbin("PC-0001"));

        await ProductionDriver.AssertErrorAsync(response, 422, "CARRIER_TYPE_MISMATCH");
        Assert.Equal(0, await CountLotsAsync("type=ELECTRODE"));
    }

    [Fact]
    public async Task Coat_on_full_bobbin_is_carrier_not_empty()
    {
        var (_, run) = await StartCoatAsync();
        await _driver.ProduceOkAsync(run.Id, OnBobbin("BB-0001"));

        var response = await _driver.ProduceAsync(run.Id, OnBobbin("BB-0002"), OnBobbin("BB-0001"));

        await ProductionDriver.AssertErrorAsync(response, 422, "CARRIER_NOT_EMPTY");
        Assert.Equal(1, await CountLotsAsync("type=ELECTRODE"));
        Assert.Equal(CarrierStatus.Empty, (await _driver.CarrierAsync("BB-0002")).Status);
    }

    [Fact]
    public async Task Coat_rejects_missing_carrier_lane_duplicate_carriers_and_zero_good()
    {
        var (_, run) = await StartCoatAsync();

        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine(null, null, 1200m, 0m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine("BB-0001", 1, 1200m, 0m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, OnBobbin("BB-0001"), OnBobbin(" bb-0001")), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, OnBobbin("BB-0001", 0m, 20m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(await _driver.ProduceAsync(run.Id), 422, "INVALID_OUTPUT_SET");
        Assert.Equal(0, await CountLotsAsync("type=ELECTRODE"));
    }

    [Fact]
    public async Task Calendering_moves_same_lot_to_new_bobbin()
    {
        var (wo, roll) = await CoatedRollAsync("BB-0001");

        var run = await _driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
        var produced = await _driver.ProduceOkAsync(run.Id, OnBobbin("BB-0004", 1180m, 20m));

        var output = Assert.Single(produced.Outputs);
        Assert.Equal(roll, output.LotId);
        Assert.Equal("BB-0004", output.CarrierCode);
        Assert.Equal(1180m, output.GoodQty);
        Assert.Equal(20m, output.RejectQty);

        var lot = await _driver.LotAsync(roll);
        Assert.Equal("BB-0004", lot.CurrentCarrier);
        Assert.Equal(1180m, lot.Qty);
        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(QualityStatus.None, lot.Quality);
        Assert.Equal(OperationCode.Cal, lot.CurrentOperation);
        Assert.Equal(OperationCode.Slit, lot.NextOperation);

        var old = await _driver.CarrierAsync("BB-0001");
        Assert.Equal(CarrierStatus.Empty, old.Status);
        Assert.Null(old.LotId);
        var fresh = await _driver.CarrierAsync("BB-0004");
        Assert.Equal(CarrierStatus.Full, fresh.Status);
        Assert.Equal(roll, fresh.LotId);

        Assert.Equal(0, await GenealogyLinkCountAsync(run.Id));
        var events = await (await _driver.OperatorAsync())
            .GetFromJsonAsync<JsonElement>($"/api/lots/{roll}/events", Ct);
        var tail = events.EnumerateArray().TakeLast(3).ToArray();
        Assert.Equal(
            ["TRACK_IN", "CARRIER_UNLOAD", "CARRIER_LOAD"], tail.Select(e => e.GetProperty("type").GetString()));
        Assert.Equal("BB-0001", tail[1].GetProperty("carrier").GetString());
        Assert.Equal("BB-0004", tail[2].GetProperty("carrier").GetString());
    }

    [Fact]
    public async Task Calendering_second_produce_or_wrong_shape_is_invalid_output_set()
    {
        var (wo, _) = await CoatedRollAsync("BB-0001");
        var run = await _driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");

        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine(null, null, 1180m, 20m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, OnBobbin("BB-0004", 0m, 20m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, new OutputLine("BB-0004", 1, 1180m, 20m)), 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(
            await _driver.ProduceAsync(run.Id, OnBobbin("BB-0004"), OnBobbin("BB-0005")), 422, "INVALID_OUTPUT_SET");
        await _driver.ProduceOkAsync(run.Id, OnBobbin("BB-0004", 1180m, 20m));

        var again = await _driver.ProduceAsync(run.Id, OnBobbin("BB-0005", 1180m, 20m));

        await ProductionDriver.AssertErrorAsync(again, 422, "INVALID_OUTPUT_SET");
        Assert.Equal(CarrierStatus.Empty, (await _driver.CarrierAsync("BB-0005")).Status);
    }

    [Fact]
    public async Task Calendering_onto_the_same_full_bobbin_is_carrier_not_empty()
    {
        var (wo, roll) = await CoatedRollAsync("BB-0001");
        var run = await _driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");

        var response = await _driver.ProduceAsync(run.Id, OnBobbin("BB-0001", 1180m, 20m));

        await ProductionDriver.AssertErrorAsync(response, 422, "CARRIER_NOT_EMPTY");
        var lot = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Run, lot.Status);
        Assert.Equal(1200m, lot.Qty);
        Assert.Equal("BB-0001", lot.CurrentCarrier);
    }

    [Fact]
    public async Task Coated_roll_into_slit_is_route_violation()
    {
        var (wo, _) = await CoatedRollAsync("BB-0001");

        var response = await _driver.TrackInAsync("SL01", wo, OperationCode.Slit, "BB-0001");

        await ProductionDriver.AssertErrorAsync(response, 422, "ROUTE_VIOLATION");
    }

    [Fact]
    public async Task Slurry_from_other_order_is_lot_wo_mismatch()
    {
        var (_, slurry) = await MixAsync();
        var other = await _driver.CreateReleasedWorkOrderAsync();
        var foil = (await _driver.MaterialLotsAsync("AL-FOIL")).Single();

        var response = await _driver.TrackInAsync("CT01", other, OperationCode.Coat, foil, slurry);

        await ProductionDriver.AssertErrorAsync(response, 422, "LOT_WO_MISMATCH");
    }

    [Fact]
    public async Task Slit_grid_creates_pancakes_skipping_zero_lanes_and_finishes_them()
    {
        var (wo, roll) = await CalenderedRollAsync();
        var run = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0004");

        var produced = await _driver.ProduceOkAsync(run.Id, Grid(zeroLane: 5));

        Assert.Equal(8, produced.Outputs.Count);
        var empty = produced.Outputs.Single(o => o.Lane == 5);
        Assert.Null(empty.LotId);
        Assert.Null(empty.CarrierCode);
        Assert.Equal(0m, empty.GoodQty);
        Assert.Equal(15m, empty.RejectQty);

        var expectedLanes = new[] { 1, 2, 3, 4, 6, 7, 8 };
        var pancakes = produced.Outputs.Where(o => o.LotId is not null).ToList();
        Assert.Equal(expectedLanes, pancakes.Select(o => o.Lane!.Value));
        Assert.Equal(expectedLanes.Select(l => $"{roll}-{l:00}"), pancakes.Select(o => o.LotId!));

        foreach (var output in pancakes)
        {
            var lot = await _driver.LotAsync(output.LotId!);
            Assert.Equal(LotType.Pancake, lot.Type);
            Assert.Equal("m", lot.Uom);
            Assert.Equal(LotStatus.Finished, lot.Status);
            Assert.Equal(QualityStatus.None, lot.Quality);
            Assert.Equal(OperationCode.Slit, lot.CurrentOperation);
            Assert.Null(lot.NextOperation);
            Assert.Equal(output.CarrierCode, lot.CurrentCarrier);
            Assert.Equal(100m, lot.Qty);

            var carrier = await _driver.CarrierAsync(output.CarrierCode!);
            Assert.Equal(CarrierStatus.Full, carrier.Status);
            Assert.Equal(output.LotId, carrier.LotId);
            Assert.Equal([roll], await ParentsOfAsync(output.LotId!));
        }

        Assert.Equal(7, await CountLotsAsync("type=PANCAKE&status=FINISHED"));
        var order = await _driver.WorkOrderAsync(wo);
        Assert.Equal(7, order.GoodCount);
        Assert.Equal(WorkOrderStatus.Running, order.Status);

        var events = await (await _driver.OperatorAsync())
            .GetFromJsonAsync<JsonElement>($"/api/lots/{pancakes[0].LotId}/events", Ct);
        Assert.Equal(
            ["CREATE", "CARRIER_LOAD", "FINISH"],
            events.EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToArray());
    }

    [Fact]
    public async Task Slit_grid_with_duplicate_carrier_is_invalid_output_set()
    {
        var (wo, _) = await CalenderedRollAsync();
        var run = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0004");
        var grid = Grid();
        grid[1] = grid[1] with { CarrierCode = " pc-0001 " };

        var response = await _driver.ProduceAsync(run.Id, grid);

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_OUTPUT_SET");
        Assert.Equal(0, await CountLotsAsync("type=PANCAKE"));
        Assert.Equal(CarrierStatus.Empty, (await _driver.CarrierAsync("PC-0001")).Status);
        Assert.Equal(0, (await _driver.WorkOrderAsync(wo)).GoodCount);
    }

    [Fact]
    public async Task Slit_grid_missing_a_lane_is_invalid_output_set()
    {
        var (wo, _) = await CalenderedRollAsync();
        var run = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0004");

        var missingLane = await _driver.ProduceAsync(run.Id, Grid().Take(7).ToArray());
        var wrongLane = await _driver.ProduceAsync(
            run.Id, Grid().Take(7).Append(new OutputLine("PC-0009", 9, 100m, 0m)).ToArray());
        var duplicateLane = await _driver.ProduceAsync(
            run.Id, Grid().Take(7).Append(new OutputLine("PC-0009", 7, 100m, 0m)).ToArray());
        var noCarrier = Grid();
        noCarrier[2] = noCarrier[2] with { CarrierCode = null };
        var withoutCarrier = await _driver.ProduceAsync(run.Id, noCarrier);

        await ProductionDriver.AssertErrorAsync(missingLane, 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(wrongLane, 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(duplicateLane, 422, "INVALID_OUTPUT_SET");
        await ProductionDriver.AssertErrorAsync(withoutCarrier, 422, "INVALID_OUTPUT_SET");
        Assert.Equal(0, await CountLotsAsync("type=PANCAKE"));
    }

    [Fact]
    public async Task Slit_grid_can_only_be_produced_once()
    {
        var (wo, _) = await CalenderedRollAsync();
        var run = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0004");
        await _driver.ProduceOkAsync(run.Id, Grid());

        var secondGrid = Grid().Select(l => l with { CarrierCode = l.CarrierCode!.Replace("PC-00", "PC-01") });
        var response = await _driver.ProduceAsync(run.Id, [.. secondGrid]);

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_OUTPUT_SET");
        Assert.Equal(8, await CountLotsAsync("type=PANCAKE"));
    }

    /// <summary>Eight lanes of 100 m on PC-0001..8; <paramref name="zeroLane"/> gets no good output and 15 m reject.</summary>
    private static OutputLine[] Grid(int? zeroLane = null) =>
        [.. Enumerable.Range(1, 8).Select(lane => lane == zeroLane
            ? new OutputLine(null, lane, 0m, 15m)
            : new OutputLine($"PC-{lane:0000}", lane, 100m, 2m))];

    private async Task<(WorkOrderDto Wo, string Slurry)> MixAsync()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var run = await _driver.TrackInOkAsync(
            "MX01", wo, OperationCode.Mix, await _driver.MaterialLotsAsync(CathodeMix));
        var produced = await _driver.ProduceOkAsync(run.Id, new OutputLine(null, null, 480m, 20m));
        return (wo, produced.Outputs.Single().LotId!);
    }

    private async Task<(WorkOrderDto Wo, RunDto Run)> StartCoatAsync()
    {
        var (wo, slurry) = await MixAsync();
        var foil = (await _driver.MaterialLotsAsync("AL-FOIL")).Single();
        return (wo, await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry));
    }

    /// <summary>A work order with one coated roll waiting on <paramref name="bobbin"/>.</summary>
    private async Task<(WorkOrderDto Wo, string Roll)> CoatedRollAsync(string bobbin)
    {
        var (wo, run) = await StartCoatAsync();
        var produced = await _driver.ProduceOkAsync(run.Id, OnBobbin(bobbin));
        return (wo, produced.Outputs.Single().LotId!);
    }

    /// <summary>A coated roll that went through calendering and now waits for slitting on BB-0004.</summary>
    private async Task<(WorkOrderDto Wo, string Roll)> CalenderedRollAsync()
    {
        var (wo, roll) = await CoatedRollAsync("BB-0001");
        var cal = await _driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
        await _driver.ProduceOkAsync(cal.Id, OnBobbin("BB-0004", 1180m, 20m));
        return (wo, roll);
    }

    private async Task<int> CountLotsAsync(string query) =>
        (await (await _driver.OperatorAsync()).GetFromJsonAsync<JsonElement>($"/api/lots?{query}", Ct)).GetArrayLength();

    private async Task<List<string>> ParentsOfAsync(string childLotId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        return await (
            from g in db.Set<GenealogyLink>().AsNoTracking()
            join child in db.Set<Lot>() on g.ChildLotId equals child.Id
            join parent in db.Set<Lot>() on g.ParentLotId equals parent.Id
            where child.LotId == childLotId
            select parent.LotId).ToListAsync(Ct);
    }

    private async Task<int> GenealogyLinkCountAsync(Guid runId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        return await db.Set<GenealogyLink>().CountAsync(g => g.RunId == runId, Ct);
    }
}
