using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniMes.Api.Modules.Carriers.Features.Queries;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;

namespace MiniMes.IntegrationTests;

/// <summary>
/// Drives the production flow through the HTTP API as the planner and operator users, so tests read as a
/// story (create order, register material, track in) instead of repeating request plumbing. Create one per
/// test, after the database has been reset.
/// </summary>
public sealed class ProductionDriver(MesApiFactory api)
{
    private const decimal RawQty = 500m;
    private const decimal FoilQty = 6000m;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    private HttpClient? _planner;
    private HttpClient? _operator;
    private Dictionary<string, MaterialDto>? _materials;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task<HttpClient> PlannerAsync() => _planner ??= await api.ClientAsAsync("planner");

    public async Task<HttpClient> OperatorAsync() => _operator ??= await api.ClientAsAsync("operator");

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
