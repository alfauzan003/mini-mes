using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Features.TrackOut;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.Quality.Features.Inspections;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;

namespace MiniMes.IntegrationTests.Quality;

[Collection("api")]
public class DispositionTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly Dictionary<string, decimal> BadWidth = new() { ["Width"] = 101m, ["Burr height"] = 4m };

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        await api.SeedInspectionSpecsAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonSerializerOptions Json => ProductionDriver.JsonOptions;

    private static Task<HttpResponseMessage> HoldAsync(HttpClient client, string lotId, string? reason) =>
        client.PostAsJsonAsync($"/api/lots/{lotId}/hold", new { reason }, Json, Ct);

    private static Task<HttpResponseMessage> DispositionAsync(
        HttpClient client, string lotId, Disposition decision, string? reason) =>
        client.PostAsJsonAsync($"/api/lots/{lotId}/disposition", new { decision, reason }, Json, Ct);

    private static async Task HoldOkAsync(ProductionDriver driver, string lotId, string reason) =>
        Assert.Equal(HttpStatusCode.OK, (await HoldAsync(await driver.QcAsync(), lotId, reason)).StatusCode);

    private static async Task<LotDto> DispositionOkAsync(ProductionDriver driver, string lotId, Disposition decision)
    {
        var response = await DispositionAsync(await driver.QcAsync(), lotId, decision, "QC decision");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LotDto>(Json, Ct))!;
    }

    private static async Task<IReadOnlyList<InspectionDto>> HistoryAsync(HttpClient client, string lotId) =>
        (await client.GetFromJsonAsync<List<InspectionDto>>($"/api/lots/{lotId}/inspections", Json, Ct))!;

    private static async Task<IReadOnlyList<LotEventDto>> EventsAsync(HttpClient client, string lotId) =>
        (await client.GetFromJsonAsync<List<LotEventDto>>($"/api/lots/{lotId}/events", Json, Ct))!;

    private static async Task<IReadOnlyList<LotDto>> QueueAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LotDto>>("/api/inspections/queue", Json, Ct))!;

    /// <summary>Mixes a slurry lot that has not been inspected.</summary>
    private static async Task<(WorkOrderDto Wo, string Slurry)> ProduceSlurryAsync(ProductionDriver driver)
    {
        var wo = await driver.CreateReleasedWorkOrderAsync();
        var raws = await driver.MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");
        var mix = await driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);
        var slurry = (await driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m))).Outputs.Single().LotId!;
        await driver.TrackOutOkAsync(mix.Id);
        return (wo, slurry);
    }

    /// <summary>Coats a roll onto <paramref name="bobbin"/> and leaves it waiting, not yet inspected.</summary>
    private static async Task<(WorkOrderDto Wo, string Roll)> ProduceCoatedRollAsync(ProductionDriver driver, string bobbin)
    {
        var (wo, slurry) = await ProduceSlurryAsync(driver);
        await driver.InspectPassAsync(slurry);
        var foil = (await driver.MaterialLotsAsync("AL-FOIL")).Single();
        var coat = await driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        var roll = (await driver.ProduceOkAsync(coat.Id, new OutputLine(bobbin, null, 1200m, 20m))).Outputs.Single().LotId!;
        await driver.TrackOutOkAsync(coat.Id, new Consumption(foil, 1300m));
        return (wo, roll);
    }

    private static Task<InspectionDto> FailLoadingAsync(ProductionDriver driver, string roll) =>
        driver.InspectOkAsync(
            roll, new Dictionary<string, decimal> { ["Loading weight"] = 25m }, "CT-LOAD", "Too heavy");

    [Fact]
    public async Task Manual_hold_requires_reason()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        var qc = await driver.QcAsync();

        var missing = await HoldAsync(qc, slurry, null);
        var blank = await HoldAsync(qc, slurry, "   ");

        await ProductionDriver.AssertErrorAsync(missing, 422, "REASON_REQUIRED");
        await ProductionDriver.AssertErrorAsync(blank, 422, "REASON_REQUIRED");
        Assert.Equal(LotStatus.Wait, (await driver.LotAsync(slurry)).Status);
    }

    [Fact]
    public async Task Manual_hold_records_event_and_operator_cannot_hold()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);

        var forbidden = await HoldAsync(await driver.OperatorAsync(), slurry, "Suspicious");
        var held = await HoldAsync(await driver.QcAsync(), slurry, "  Suspicious colour ");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        var lot = (await held.Content.ReadFromJsonAsync<LotDto>(Json, Ct))!;
        Assert.Equal(LotStatus.Hold, lot.Status);
        var hold = Assert.Single(await EventsAsync(await driver.OperatorAsync(), slurry), e => e.Type == LotEventType.Hold);
        Assert.Equal("Suspicious colour", hold.Note);
    }

    [Fact]
    public async Task Holding_unknown_lot_is_not_found_and_holding_a_held_lot_is_not_available()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        var qc = await driver.QcAsync();
        await HoldOkAsync(driver, slurry, "First");

        await ProductionDriver.AssertErrorAsync(await HoldAsync(qc, "SL-NOPE", "Why"), 404, "LOT_NOT_FOUND");
        await ProductionDriver.AssertErrorAsync(await HoldAsync(qc, slurry, "Again"), 422, "LOT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Manual_hold_then_release_keeps_quality_none()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        await HoldOkAsync(driver, slurry, "Check colour");

        var lot = await DispositionOkAsync(driver, slurry, Disposition.Release);

        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(QualityStatus.None, lot.Quality);
        var qc = await driver.QcAsync();
        Assert.Contains(await QueueAsync(qc), l => l.LotId == slurry);
        Assert.Contains(await EventsAsync(qc, slurry), e => e.Type == LotEventType.Release && e.Note == "QC decision");
    }

    [Fact]
    public async Task Held_lot_cannot_be_tracked_in()
    {
        var driver = new ProductionDriver(api);
        var (wo, slurry) = await ProduceSlurryAsync(driver);
        await driver.InspectPassAsync(slurry);
        await HoldOkAsync(driver, slurry, "Hold it");
        var foil = (await driver.MaterialLotsAsync("AL-FOIL")).Single();

        var response = await driver.TrackInAsync("CT01", wo, OperationCode.Coat, foil, slurry);

        await ProductionDriver.AssertErrorAsync(response, 422, "LOT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Release_of_failed_roll_passes_it_and_records_disposition()
    {
        var driver = new ProductionDriver(api);
        var (wo, roll) = await ProduceCoatedRollAsync(driver, "BB-0001");
        await FailLoadingAsync(driver, roll);

        var lot = await DispositionOkAsync(driver, roll, Disposition.Release);

        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(QualityStatus.Pass, lot.Quality);
        var qc = await driver.QcAsync();
        var inspection = Assert.Single(await HistoryAsync(qc, roll));
        Assert.Equal(Disposition.Release, inspection.Disposition);
        Assert.Equal("qc", inspection.DispositionBy);
        Assert.Equal("QC decision", inspection.DispositionReason);
        Assert.NotNull(inspection.DispositionAt);
        Assert.Contains(await EventsAsync(qc, roll), e => e.Type == LotEventType.Release);
        await driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
    }

    [Fact]
    public async Task Disposition_requires_reason_and_stores_nothing()
    {
        var driver = new ProductionDriver(api);
        var (_, roll) = await ProduceCoatedRollAsync(driver, "BB-0001");
        await FailLoadingAsync(driver, roll);
        var qc = await driver.QcAsync();

        var missing = await DispositionAsync(qc, roll, Disposition.Release, null);
        var blank = await DispositionAsync(qc, roll, Disposition.Scrap, "  ");

        await ProductionDriver.AssertErrorAsync(missing, 422, "REASON_REQUIRED");
        await ProductionDriver.AssertErrorAsync(blank, 422, "REASON_REQUIRED");
        Assert.Null(Assert.Single(await HistoryAsync(qc, roll)).Disposition);
        Assert.Equal(LotStatus.Hold, (await driver.LotAsync(roll)).Status);
    }

    [Theory]
    [InlineData("""{"reason":"Because"}""")]
    [InlineData("""{"decision":null,"reason":"Because"}""")]
    [InlineData("""{"decision":7,"reason":"Because"}""")]
    [InlineData("""{"decision":-1,"reason":"Because"}""")]
    [InlineData("""{"decision":"MAYBE","reason":"Because"}""")]
    public async Task Missing_or_unknown_decision_is_rejected_and_the_lot_stays_held(string body)
    {
        var driver = new ProductionDriver(api);
        var (_, roll) = await ProduceCoatedRollAsync(driver, "BB-0001");
        await FailLoadingAsync(driver, roll);
        var qc = await driver.QcAsync();

        var response = await qc.PostAsync(
            $"/api/lots/{roll}/disposition", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var lot = await driver.LotAsync(roll);
        Assert.Equal(LotStatus.Hold, lot.Status);
        Assert.Equal(QualityStatus.Fail, lot.Quality);
        Assert.Null(Assert.Single(await HistoryAsync(qc, roll)).Disposition);
        Assert.DoesNotContain(
            await EventsAsync(qc, roll), e => e.Type is LotEventType.Release or LotEventType.Scrap);
    }

    [Fact]
    public async Task Operator_cannot_disposition()
    {
        var driver = new ProductionDriver(api);
        var (_, roll) = await ProduceCoatedRollAsync(driver, "BB-0001");
        await FailLoadingAsync(driver, roll);

        var response = await DispositionAsync(await driver.OperatorAsync(), roll, Disposition.Release, "Because");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Scrap_frees_the_carrier()
    {
        var driver = new ProductionDriver(api);
        var (_, roll) = await ProduceCoatedRollAsync(driver, "BB-0001");
        await FailLoadingAsync(driver, roll);
        Assert.Equal(CarrierStatus.Full, (await driver.CarrierAsync("BB-0001")).Status);

        var lot = await DispositionOkAsync(driver, roll, Disposition.Scrap);

        Assert.Equal(LotStatus.Scrapped, lot.Status);
        Assert.Null(lot.CurrentCarrier);
        var carrier = await driver.CarrierAsync("BB-0001");
        Assert.Equal(CarrierStatus.Empty, carrier.Status);
        Assert.Null(carrier.LotId);
        var qc = await driver.QcAsync();
        Assert.Equal(Disposition.Scrap, Assert.Single(await HistoryAsync(qc, roll)).Disposition);
        var events = (await EventsAsync(qc, roll)).ToList();
        Assert.Equal("BB-0001", Assert.Single(events, e => e.Type == LotEventType.CarrierUnload).Carrier);
        Assert.True(
            events.FindIndex(e => e.Type == LotEventType.CarrierUnload) < events.FindIndex(e => e.Type == LotEventType.Scrap));

        var (_, another) = await ProduceCoatedRollAsync(driver, "BB-0001");
        Assert.Equal("BB-0001", (await driver.LotAsync(another)).CurrentCarrier);
    }

    [Fact]
    public async Task Manual_hold_then_scrap_works_without_an_inspection()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        await HoldOkAsync(driver, slurry, "Contaminated");

        var lot = await DispositionOkAsync(driver, slurry, Disposition.Scrap);

        Assert.Equal(LotStatus.Scrapped, lot.Status);
        Assert.Empty(await HistoryAsync(await driver.QcAsync(), slurry));
    }

    [Fact]
    public async Task Released_failed_pancake_counts_toward_work_order()
    {
        var driver = new ProductionDriver(api);
        var flow = await driver.RunFullFlowAsync(1, driver.InspectPassAsync);
        var pancake = flow.Pancakes.Single();
        await driver.InspectOkAsync(pancake, BadWidth, "SL-WIDTH", "Too wide");
        Assert.Equal(0, (await driver.WorkOrderAsync(flow.WorkOrder)).GoodCount);

        var lot = await DispositionOkAsync(driver, pancake, Disposition.Release);

        Assert.Equal(LotStatus.Finished, lot.Status);
        Assert.Equal(QualityStatus.Pass, lot.Quality);
        var wo = await driver.WorkOrderAsync(flow.WorkOrder);
        Assert.Equal(1, wo.GoodCount);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        var events = await EventsAsync(await driver.QcAsync(), pancake);
        Assert.Contains(events, e => e.Type == LotEventType.Release);
        Assert.Contains(events, e => e.Type == LotEventType.Finish);
    }

    [Fact]
    public async Task Released_manually_held_pancake_is_not_finished()
    {
        var driver = new ProductionDriver(api);
        var flow = await driver.RunFullFlowAsync(1, driver.InspectPassAsync);
        var pancake = flow.Pancakes.Single();
        await HoldOkAsync(driver, pancake, "Check width");

        var lot = await DispositionOkAsync(driver, pancake, Disposition.Release);

        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(QualityStatus.None, lot.Quality);
        Assert.Equal(0, (await driver.WorkOrderAsync(flow.WorkOrder)).GoodCount);
    }

    [Fact]
    public async Task Scrapped_pancake_does_not_count()
    {
        var driver = new ProductionDriver(api);
        var flow = await driver.RunFullFlowAsync(8, driver.InspectPassAsync);
        await driver.InspectOkAsync(flow.Pancakes[0], BadWidth, "SL-WIDTH", "Too wide");
        await DispositionOkAsync(driver, flow.Pancakes[0], Disposition.Scrap);

        foreach (var pancake in flow.Pancakes.Skip(1))
        {
            await driver.InspectPassAsync(pancake);
        }

        var wo = await driver.WorkOrderAsync(flow.WorkOrder);
        Assert.Equal(WorkOrderStatus.Running, wo.Status);
        Assert.Equal(7, wo.GoodCount);
        Assert.Equal(LotStatus.Scrapped, (await driver.LotAsync(flow.Pancakes[0])).Status);
    }

    [Fact]
    public async Task Disposition_of_non_held_lot_is_lot_not_available()
    {
        var driver = new ProductionDriver(api);
        var (_, slurry) = await ProduceSlurryAsync(driver);
        var qc = await driver.QcAsync();

        var release = await DispositionAsync(qc, slurry, Disposition.Release, "Why not");
        var scrap = await DispositionAsync(qc, slurry, Disposition.Scrap, "Why not");

        await ProductionDriver.AssertErrorAsync(release, 422, "LOT_NOT_AVAILABLE");
        await ProductionDriver.AssertErrorAsync(scrap, 422, "LOT_NOT_AVAILABLE");
        Assert.Equal(LotStatus.Wait, (await driver.LotAsync(slurry)).Status);
        await ProductionDriver.AssertErrorAsync(
            await DispositionAsync(qc, "SL-NOPE", Disposition.Release, "Why"), 404, "LOT_NOT_FOUND");
    }

    [Fact]
    public async Task Inspection_history_is_newest_first_after_release_and_reinspection()
    {
        var driver = new ProductionDriver(api);
        var (wo, roll) = await ProduceCoatedRollAsync(driver, "BB-0001");
        await FailLoadingAsync(driver, roll);
        await DispositionOkAsync(driver, roll, Disposition.Release);
        var cal = await driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
        await driver.ProduceOkAsync(cal.Id, new OutputLine("BB-0002", null, 1180m, 20m));
        await driver.TrackOutOkAsync(cal.Id);
        await driver.InspectPassAsync(roll);

        var history = await HistoryAsync(await driver.QcAsync(), roll);

        Assert.Equal(2, history.Count);
        Assert.Equal([OperationCode.Cal, OperationCode.Coat], history.Select(i => i.Operation));
        Assert.Equal([InspectionResult.Pass, InspectionResult.Fail], history.Select(i => i.Result));
        Assert.True(history[0].InspectedAt > history[1].InspectedAt);
    }
}
