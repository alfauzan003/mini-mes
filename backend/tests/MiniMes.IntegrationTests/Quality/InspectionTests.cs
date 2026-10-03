using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Quality;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.Quality.Features.Inspections;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;

namespace MiniMes.IntegrationTests.Quality;

[Collection("api")]
public class InspectionTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly Dictionary<string, decimal> GoodSlurry = new() { ["Viscosity"] = 6000m, ["Solid content"] = 70m };

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        await api.SeedInspectionSpecsAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonSerializerOptions Json => ProductionDriver.JsonOptions;

    /// <summary>Mixes a slurry lot (not yet inspected) on a fresh released order.</summary>
    private static async Task<(WorkOrderDto Wo, string Slurry)> ProduceSlurryAsync(ProductionDriver driver)
    {
        var wo = await driver.CreateReleasedWorkOrderAsync();
        var raws = await driver.MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");
        var mix = await driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);
        var slurry = (await driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m))).Outputs.Single().LotId!;
        await driver.TrackOutOkAsync(mix.Id);
        return (wo, slurry);
    }

    private static async Task<IReadOnlyList<InspectionDto>> HistoryAsync(HttpClient client, string lotId) =>
        (await client.GetFromJsonAsync<List<InspectionDto>>($"/api/lots/{lotId}/inspections", Json, Ct))!;

    private static async Task<IReadOnlyList<LotDto>> QueueAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LotDto>>("/api/inspections/queue", Json, Ct))!;

    [Fact]
    public async Task Slurry_appears_in_queue_and_passes_inspection()
    {
        var driver = new ProductionDriver(api);
        var (wo, slurry) = await ProduceSlurryAsync(driver);
        var qc = await driver.QcAsync();
        Assert.Contains(await QueueAsync(qc), l => l.LotId == slurry);

        var inspection = await driver.InspectOkAsync(slurry, GoodSlurry);

        Assert.Equal(InspectionResult.Pass, inspection.Result);
        Assert.Equal(slurry, inspection.LotId);
        Assert.Equal(OperationCode.Mix, inspection.Operation);
        Assert.Equal("qc", inspection.Inspector);
        Assert.Equal(["Viscosity", "Solid content"], inspection.Measurements.Select(m => m.ItemName));
        Assert.All(inspection.Measurements, m => Assert.Equal(Judgment.Ok, m.Judgment));
        Assert.Equal(QualityStatus.Pass, (await driver.LotAsync(slurry)).Quality);
        Assert.DoesNotContain(await QueueAsync(qc), l => l.LotId == slurry);

        var foil = (await driver.MaterialLotsAsync("AL-FOIL")).Single();
        await driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
    }

    [Fact]
    public async Task Out_of_spec_value_fails_and_holds_lot()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);

        var inspection = await driver.InspectOkAsync(
            slurry, new Dictionary<string, decimal> { ["Viscosity"] = 9000m, ["Solid content"] = 70m },
            "MX-VISC", "Too thick");

        Assert.Equal(InspectionResult.Fail, inspection.Result);
        Assert.Equal("MX-VISC", inspection.DefectCode);
        Assert.Equal("Viscosity out of spec", inspection.DefectDescription);
        Assert.Equal("Too thick", inspection.Reason);
        Assert.Equal(Judgment.Ng, inspection.Measurements.Single(m => m.ItemName == "Viscosity").Judgment);
        var lot = await driver.LotAsync(slurry);
        Assert.Equal(LotStatus.Hold, lot.Status);
        Assert.Equal(QualityStatus.Fail, lot.Quality);

        var events = (await (await driver.OperatorAsync())
            .GetFromJsonAsync<List<LotEventDto>>($"/api/lots/{slurry}/events", Json, Ct))!;
        var inspect = Assert.Single(events, e => e.Type == LotEventType.Inspect);
        Assert.Equal("FAIL MX-VISC: Too thick", inspect.Note);
        Assert.Contains(events, e => e.Type == LotEventType.Hold);
    }

    [Fact]
    public async Task Failure_reason_over_the_limit_is_rejected_and_stores_nothing()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);

        var response = await driver.InspectAsync(
            slurry, new Dictionary<string, decimal> { ["Viscosity"] = 9000m, ["Solid content"] = 70m },
            "MX-VISC", new string('x', ReasonRules.MaxLength + 1));

        await ProductionDriver.AssertErrorAsync(response, 422, "REASON_TOO_LONG");
        Assert.Empty(await HistoryAsync(await driver.QcAsync(), slurry));
        Assert.Equal(LotStatus.Wait, (await driver.LotAsync(slurry)).Status);
    }

    [Fact]
    public async Task Failure_reason_at_the_limit_with_the_longest_defect_code_is_stored()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        var reason = new string('x', ReasonRules.MaxLength);

        var inspection = await driver.InspectOkAsync(
            slurry, new Dictionary<string, decimal> { ["Viscosity"] = 9000m, ["Solid content"] = 70m },
            "MX-SOLID", reason);

        Assert.Equal(reason, inspection.Reason);
        var events = (await (await driver.OperatorAsync())
            .GetFromJsonAsync<List<LotEventDto>>($"/api/lots/{slurry}/events", Json, Ct))!;
        Assert.Equal($"FAIL MX-SOLID: {reason}", Assert.Single(events, e => e.Type == LotEventType.Inspect).Note);
    }

    [Fact]
    public async Task Failure_without_defect_is_defect_required_and_stores_nothing()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);

        var response = await driver.InspectAsync(
            slurry, new Dictionary<string, decimal> { ["Viscosity"] = 9000m, ["Solid content"] = 70m });

        await ProductionDriver.AssertErrorAsync(response, 422, "DEFECT_REQUIRED");
        Assert.Empty(await HistoryAsync(await driver.QcAsync(), slurry));
        Assert.Equal(LotStatus.Wait, (await driver.LotAsync(slurry)).Status);
    }

    [Fact]
    public async Task Unknown_defect_code_is_defect_code_not_found()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);

        var response = await driver.InspectAsync(
            slurry, new Dictionary<string, decimal> { ["Viscosity"] = 9000m, ["Solid content"] = 70m },
            "NOPE", "Too thick");

        await ProductionDriver.AssertErrorAsync(response, 422, "DEFECT_CODE_NOT_FOUND");
    }

    [Fact]
    public async Task Inspecting_twice_is_lot_not_available()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        await driver.InspectOkAsync(slurry, GoodSlurry);

        var second = await driver.InspectAsync(slurry, GoodSlurry);

        await ProductionDriver.AssertErrorAsync(second, 422, "LOT_NOT_AVAILABLE");
        Assert.Single(await HistoryAsync(await driver.QcAsync(), slurry));
    }

    [Fact]
    public async Task Missing_measurement_is_invalid_measurements_and_stores_nothing()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        var specs = await driver.SpecsOfAsync(slurry);
        var qc = await driver.QcAsync();

        var missing = await qc.PostAsJsonAsync(
            $"/api/lots/{slurry}/inspections",
            new { measurements = new[] { new { specId = specs[0].Id, value = 6000m } } }, Json, Ct);
        var extra = await qc.PostAsJsonAsync(
            $"/api/lots/{slurry}/inspections",
            new
            {
                measurements = new[]
                {
                    new { specId = specs[0].Id, value = 6000m }, new { specId = specs[1].Id, value = 70m },
                    new { specId = Guid.NewGuid(), value = 1m }
                }
            }, Json, Ct);
        var duplicate = await qc.PostAsJsonAsync(
            $"/api/lots/{slurry}/inspections",
            new { measurements = new[] { new { specId = specs[0].Id, value = 6000m }, new { specId = specs[0].Id, value = 6000m } } },
            Json, Ct);

        await ProductionDriver.AssertErrorAsync(missing, 422, "INVALID_MEASUREMENTS");
        await ProductionDriver.AssertErrorAsync(extra, 422, "INVALID_MEASUREMENTS");
        await ProductionDriver.AssertErrorAsync(duplicate, 422, "INVALID_MEASUREMENTS");
        Assert.Empty(await HistoryAsync(qc, slurry));
        var lot = await driver.LotAsync(slurry);
        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(QualityStatus.None, lot.Quality);
    }

    [Fact]
    public async Task Operator_cannot_inspect()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);

        var response = await driver.InspectAsAsync(await driver.OperatorAsync(), slurry, GoodSlurry);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Raw_lot_has_no_inspection_spec()
    {
        var driver = new ProductionDriver(api);
        var raw = (await driver.MaterialLotsAsync("NCM811")).Single();

        var response = await (await driver.QcAsync()).PostAsJsonAsync(
            $"/api/lots/{raw}/inspections", new { measurements = Array.Empty<object>() }, Json, Ct);

        await ProductionDriver.AssertErrorAsync(response, 422, "NO_INSPECTION_SPEC");
    }

    [Fact]
    public async Task Unknown_lot_is_not_found()
    {
        var driver = new ProductionDriver(api);
        var qc = await driver.QcAsync();

        var post = await qc.PostAsJsonAsync(
            "/api/lots/SL-NOPE/inspections", new { measurements = Array.Empty<object>() }, Json, Ct);
        var get = await qc.GetAsync("/api/lots/SL-NOPE/inspections", Ct);

        await ProductionDriver.AssertErrorAsync(post, 404, "LOT_NOT_FOUND");
        await ProductionDriver.AssertErrorAsync(get, 404, "LOT_NOT_FOUND");
    }

    [Fact]
    public async Task Passing_pancakes_finish_and_complete_the_work_order()
    {
        var driver = new ProductionDriver(api);

        var flow = await driver.RunFullFlowAsync(8, driver.InspectPassAsync);

        Assert.Equal(8, flow.Pancakes.Length);
        var before = await driver.WorkOrderAsync(flow.WorkOrder);
        Assert.Equal(0, before.GoodCount);
        foreach (var pancake in flow.Pancakes)
        {
            var lot = await driver.LotAsync(pancake);
            Assert.Equal(LotStatus.Wait, lot.Status);
            Assert.Equal(QualityStatus.None, lot.Quality);
        }

        foreach (var pancake in flow.Pancakes)
        {
            await driver.InspectPassAsync(pancake);
        }

        foreach (var pancake in flow.Pancakes)
        {
            Assert.Equal(LotStatus.Finished, (await driver.LotAsync(pancake)).Status);
        }

        var after = await driver.WorkOrderAsync(flow.WorkOrder);
        Assert.Equal(8, after.GoodCount);
        Assert.Equal(WorkOrderStatus.Completed, after.Status);

        var events = (await (await driver.OperatorAsync())
            .GetFromJsonAsync<List<LotEventDto>>($"/api/lots/{flow.Pancakes[0]}/events", Json, Ct))!;
        Assert.Contains(events, e => e.Type == LotEventType.Finish);
    }

    [Fact]
    public async Task History_keeps_limits_after_spec_change()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        var specs = await driver.SpecsOfAsync(slurry);
        await driver.InspectOkAsync(
            slurry, new Dictionary<string, decimal> { ["Viscosity"] = 7900m, ["Solid content"] = 70m });
        var qc = await driver.QcAsync();
        var viscosity = specs.Single(s => s.ItemName == "Viscosity");

        var update = await qc.PutAsJsonAsync($"/api/specs/{viscosity.Id}", new { lsl = 4000m, usl = 7000m }, Ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var inspection = Assert.Single(await HistoryAsync(qc, slurry));
        Assert.Equal(InspectionResult.Pass, inspection.Result);
        var measurement = inspection.Measurements.Single(m => m.ItemName == "Viscosity");
        Assert.Equal(8000m, measurement.Usl);
        Assert.Equal(4000m, measurement.Lsl);
        Assert.Equal(Judgment.Ok, measurement.Judgment);
    }

    [Fact]
    public async Task Concurrent_inspections_let_exactly_one_win()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var driver = new ProductionDriver(api);
            var (_, slurry) = await ProduceSlurryAsync(driver);
            var first = await api.ClientAsAsync("qc");
            var second = await api.ClientAsAsync("qc");

            var responses = await Task.WhenAll(
                driver.InspectAsAsync(first, slurry, GoodSlurry),
                driver.InspectAsAsync(second, slurry, GoodSlurry));

            var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var loser = responses.Single(r => r != winner);
            var body = await loser.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var code = body.GetProperty("errorCode").GetString();
            Assert.True(
                (loser.StatusCode == HttpStatusCode.Conflict && code == "CONCURRENCY_CONFLICT")
                    || (loser.StatusCode == HttpStatusCode.UnprocessableEntity && code == "LOT_NOT_AVAILABLE"),
                $"Unexpected loser response {(int)loser.StatusCode} {code}.");
            Assert.Single(await HistoryAsync(first, slurry));
            Assert.Equal(QualityStatus.Pass, (await driver.LotAsync(slurry)).Quality);
        }
    }

    [Fact]
    public async Task Queue_excludes_lots_without_specs_and_lists_oldest_first()
    {
        var driver = new ProductionDriver(api);
        var raw = (await driver.MaterialLotsAsync("NCM811")).Single();
        var (_, slurry) = await ProduceSlurryAsync(driver);
        var qc = await driver.QcAsync();

        var queue = await QueueAsync(qc);

        Assert.DoesNotContain(queue, l => l.LotId == raw);
        Assert.Equal([slurry], queue.Select(l => l.LotId));
    }
}
