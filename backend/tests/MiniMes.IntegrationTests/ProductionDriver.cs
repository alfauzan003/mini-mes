using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using MiniMes.Api.Modules.Carriers.Features.Queries;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Features.TrackOut;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.Quality.Features.Inspections;
using MiniMes.Api.Modules.Quality.Features.RecordInspection;
using MiniMes.Api.Modules.Quality.Features.Specs;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;

namespace MiniMes.IntegrationTests;

/// <summary>The work order and the lots a full cathode flow left behind.</summary>
/// <param name="Raws">The four RAW lots mixed into the slurry.</param>
/// <param name="Electrode">The coated roll, which keeps its lot ID through calendering.</param>
/// <param name="Pancakes">One lot per slitting lane.</param>
public sealed record FullFlow(
    WorkOrderDto WorkOrder, string[] Raws, string Foil, string Slurry, string Electrode, string[] Pancakes,
    Guid SlitRunId);

/// <summary>
/// Drives the production flow through the HTTP API as the planner and operator users, so tests read as a
/// story (create order, register material, track in) instead of repeating request plumbing. Create one per
/// test, after the database has been reset.
/// </summary>
public sealed class ProductionDriver(WebApplicationFactory<Program> api)
{
    private const decimal RawQty = 500m;
    private const decimal FoilQty = 6000m;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    private HttpClient? _planner;
    private HttpClient? _operator;
    private HttpClient? _qc;
    private Dictionary<string, MaterialDto>? _materials;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task<HttpClient> PlannerAsync() => _planner ??= await api.ClientAsAsync("planner");

    public async Task<HttpClient> OperatorAsync() => _operator ??= await api.ClientAsAsync("operator");

    public async Task<HttpClient> QcAsync() => _qc ??= await api.ClientAsAsync("qc");

    public static JsonSerializerOptions JsonOptions => Json;

    /// <summary>Creates a planned (not yet released) work order whose route runs on the given equipment.</summary>
    public async Task<WorkOrderDto> CreateWorkOrderAsync(
        string product = "CATH-NCM811", int target = 8, string mix = "MX01", string coat = "CT01",
        string cal = "CP01", string slit = "SL01")
    {
        var request = new
        {
            productCode = product,
            targetQty = target,
            plannedStart = "2026-10-05T01:00:00Z",
            plannedEnd = "2026-10-06T01:00:00Z",
            operations = new[]
            {
                new { operation = "MIX", equipmentCode = mix },
                new { operation = "COAT", equipmentCode = coat },
                new { operation = "CAL", equipmentCode = cal },
                new { operation = "SLIT", equipmentCode = slit }
            }
        };

        var response = await (await PlannerAsync()).PostAsJsonAsync("/api/work-orders", request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkOrderDto>(Json, Ct))!;
    }

    public async Task<WorkOrderDto> CreateReleasedWorkOrderAsync(
        string product = "CATH-NCM811", int target = 8, string mix = "MX01", string coat = "CT01",
        string cal = "CP01", string slit = "SL01")
    {
        var created = await CreateWorkOrderAsync(product, target, mix, coat, cal, slit);
        return await ChangeStatusAsync(created, "release");
    }

    /// <summary>Posts release, hold, resume or complete for the order as the planner.</summary>
    public async Task<WorkOrderDto> ChangeStatusAsync(WorkOrderDto workOrder, string action)
    {
        var response = await (await PlannerAsync()).PostAsync($"/api/work-orders/{workOrder.Id}/{action}", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkOrderDto>(Json, Ct))!;
    }

    public async Task<WorkOrderDto> WorkOrderAsync(WorkOrderDto workOrder) =>
        (await (await OperatorAsync()).GetFromJsonAsync<WorkOrderDto>($"/api/work-orders/{workOrder.Id}", Json, Ct))!;

    /// <summary>
    /// Registers one fresh lot per material code (500 kg of RAW, 6000 m of FOIL, same as the seed)
    /// and returns the lot IDs in the same order.
    /// </summary>
    public async Task<string[]> MaterialLotsAsync(params string[] materialCodes)
    {
        var planner = await PlannerAsync();
        _materials ??= (await planner.GetFromJsonAsync<List<MaterialDto>>("/api/materials", Json, Ct))!
            .ToDictionary(m => m.Code);

        var lotIds = new List<string>();
        foreach (var code in materialCodes)
        {
            var qty = _materials[code].Kind == LotType.Foil ? FoilQty : RawQty;
            var response = await planner.PostAsJsonAsync("/api/lots/materials", new { materialCode = code, qty }, Ct);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            lotIds.Add((await response.Content.ReadFromJsonAsync<LotDto>(Json, Ct))!.LotId);
        }

        return [.. lotIds];
    }

    /// <summary>
    /// Runs a fresh order through MIX, COAT, CAL and SLIT with a track-out after each, slitting the roll into
    /// <paramref name="target"/> lanes (use 8 or fewer, the slitter's width). Mixing uses 100 kg of each RAW lot
    /// and coating 1300 m of foil, so those lots go back to WAIT with 400 kg and 4700 m left.
    /// </summary>
    public async Task<FullFlow> RunFullFlowAsync(int target, Func<string, Task>? afterProduce = null)
    {
        var wo = await CreateReleasedWorkOrderAsync(target: target);
        var raws = await MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");
        var foil = (await MaterialLotsAsync("AL-FOIL")).Single();

        // MIX: 4 RAW lots become one slurry lot.
        var mix = await TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);
        var slurry = (await ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m))).Outputs.Single().LotId!;
        await AfterAsync(afterProduce, slurry);
        await TrackOutOkAsync(mix.Id, [.. raws.Select(raw => new Consumption(raw, 100m))]);

        // COAT: foil and slurry become one roll on BB-0001; the slurry is used up.
        var coat = await TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        var electrode = (await ProduceOkAsync(coat.Id, new OutputLine("BB-0001", null, 1200m, 20m)))
            .Outputs.Single().LotId!;
        await AfterAsync(afterProduce, electrode);
        await TrackOutOkAsync(coat.Id, new Consumption(foil, 1300m));

        // CAL: the same roll moves from BB-0001 to BB-0002.
        var cal = await TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
        await ProduceOkAsync(cal.Id, new OutputLine("BB-0002", null, 1180m, 20m));
        await AfterAsync(afterProduce, electrode);
        await TrackOutOkAsync(cal.Id);

        // SLIT: one 145 m lane per good pancake on cores PC-0001...
        var slit = await TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0002");
        var pancakes = (await ProduceOkAsync(
                slit.Id, [.. Enumerable.Range(1, target).Select(lane => new OutputLine($"PC-{lane:0000}", lane, 145m, 2m))]))
            .Outputs.Select(o => o.LotId!).ToArray();
        await TrackOutOkAsync(slit.Id);

        return new FullFlow(wo, raws, foil, slurry, electrode, pancakes, slit.Id);
    }

    private static async Task AfterAsync(Func<string, Task>? callback, string lotId)
    {
        if (callback is not null)
        {
            await callback(lotId);
        }
    }

    /// <summary>Records an inspection as QC, giving a value per spec item name of the lot's product and operation.</summary>
    public async Task<HttpResponseMessage> InspectAsync(
        string lotId, IReadOnlyDictionary<string, decimal> valuesByItem, string? defect = null,
        string? reason = null, decimal? rejectQty = null) =>
        await InspectAsAsync(await QcAsync(), lotId, valuesByItem, defect, reason, rejectQty);

    /// <summary>Same as <see cref="InspectAsync"/> but posted by the given client.</summary>
    public async Task<HttpResponseMessage> InspectAsAsync(
        HttpClient client, string lotId, IReadOnlyDictionary<string, decimal> valuesByItem, string? defect = null,
        string? reason = null, decimal? rejectQty = null)
    {
        var specs = await SpecsOfAsync(lotId);
        var measurements = specs.Select(s => new MeasurementInput(s.Id, valuesByItem[s.ItemName])).ToList();
        return await client.PostAsJsonAsync(
            $"/api/lots/{lotId}/inspections",
            new RecordInspectionRequest(measurements, defect, reason, rejectQty), Json, Ct);
    }

    public async Task<InspectionDto> InspectOkAsync(
        string lotId, IReadOnlyDictionary<string, decimal> valuesByItem, string? defect = null,
        string? reason = null, decimal? rejectQty = null)
    {
        var response = await InspectAsync(lotId, valuesByItem, defect, reason, rejectQty);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InspectionDto>(Json, Ct))!;
    }

    /// <summary>Passes the lot's inspection with the midpoint of every spec's limits.</summary>
    public async Task InspectPassAsync(string lotId)
    {
        var specs = await SpecsOfAsync(lotId);
        var values = specs.ToDictionary(s => s.ItemName, s => (s.Lsl + s.Usl) / 2m);
        var inspection = await InspectOkAsync(lotId, values);
        Assert.Equal(InspectionResult.Pass, inspection.Result);
    }

    public async Task<IReadOnlyList<InspectionSpecDto>> SpecsOfAsync(string lotId)
    {
        var lot = await LotAsync(lotId);
        var url = $"/api/specs?product={lot.ProductCode}&operation={lot.CurrentOperation}";
        return (await (await QcAsync()).GetFromJsonAsync<List<InspectionSpecDto>>(url, Json, Ct))!;
    }

    public async Task<HttpResponseMessage> TrackInAsync(
        string equipment, WorkOrderDto workOrder, OperationCode operation, params string[] scans)
    {
        var request = new
        {
            equipmentCode = equipment,
            workOrderOperationId = workOrder.Operations.Single(o => o.Operation == operation).Id,
            inputs = scans
        };
        return await (await OperatorAsync()).PostAsJsonAsync("/api/runs/track-in", request, Ct);
    }

    public async Task<RunDto> TrackInOkAsync(
        string equipment, WorkOrderDto workOrder, OperationCode operation, params string[] scans)
    {
        var response = await TrackInAsync(equipment, workOrder, operation, scans);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RunDto>(Json, Ct))!;
    }

    public async Task<HttpResponseMessage> ProduceAsync(Guid runId, params OutputLine[] lines) =>
        await (await OperatorAsync()).PostAsJsonAsync(
            $"/api/runs/{runId}/outputs", new ProduceOutputRequest(lines), Json, Ct);

    public async Task<RunDto> ProduceOkAsync(Guid runId, params OutputLine[] lines)
    {
        var response = await ProduceAsync(runId, lines);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RunDto>(Json, Ct))!;
    }

    /// <summary>Ends the run; an input lot that is not listed is used up entirely.</summary>
    public async Task<HttpResponseMessage> TrackOutAsync(Guid runId, params Consumption[] consumptions) =>
        await (await OperatorAsync()).PostAsJsonAsync(
            $"/api/runs/{runId}/track-out", new TrackOutRequest(consumptions), Json, Ct);

    public async Task<RunDto> TrackOutOkAsync(Guid runId, params Consumption[] consumptions)
    {
        var response = await TrackOutAsync(runId, consumptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RunDto>(Json, Ct))!;
    }

    public async Task<RunDto> RunAsync(Guid runId) =>
        (await (await OperatorAsync()).GetFromJsonAsync<RunDto>($"/api/runs/{runId}", Json, Ct))!;

    public async Task<CarrierDto> CarrierAsync(string code) =>
        (await (await OperatorAsync()).GetFromJsonAsync<CarrierDto>($"/api/carriers/{code}", Json, Ct))!;

    public async Task<LotDto> LotAsync(string lotId) =>
        (await (await OperatorAsync()).GetFromJsonAsync<LotDto>($"/api/lots/{lotId}", Json, Ct))!;

    public async Task<EquipmentDto> EquipmentAsync(string code) =>
        (await (await OperatorAsync()).GetFromJsonAsync<EquipmentDto>($"/api/equipment/{code}", Json, Ct))!;

    public static async Task AssertErrorAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(code, body.GetProperty("errorCode").GetString());
    }
}
