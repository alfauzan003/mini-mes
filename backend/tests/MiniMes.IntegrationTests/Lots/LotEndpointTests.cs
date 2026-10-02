using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Shared.Data;

namespace MiniMes.IntegrationTests.Lots;

[Collection("api")]
public class LotEndpointTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static IEnumerable<string?> LotIds(JsonElement lots) =>
        lots.EnumerateArray().Select(l => l.GetProperty("lotId").GetString());

    [Fact]
    public async Task Planner_registers_foil_lot()
    {
        var client = await api.ClientAsAsync("planner");

        var response = await client.PostAsJsonAsync("/api/lots/materials", new { materialCode = "AL-FOIL", qty = 6000 }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lot = await ReadAsync(response);
        var lotId = lot.GetProperty("lotId").GetString()!;
        Assert.Matches(@"^FC-\d{6}-\d{3}$", lotId);
        Assert.Equal($"/api/lots/{lotId}", response.Headers.Location?.OriginalString);
        Assert.Equal("FOIL", lot.GetProperty("type").GetString());
        Assert.Equal("CATHODE", lot.GetProperty("polarity").GetString());
        Assert.Equal("AL-FOIL", lot.GetProperty("materialCode").GetString());
        Assert.Equal(6000m, lot.GetProperty("qty").GetDecimal());
        Assert.Equal("m", lot.GetProperty("uom").GetString());
        Assert.Equal("WAIT", lot.GetProperty("status").GetString());
        Assert.Equal("PASS", lot.GetProperty("quality").GetString());
        Assert.Equal("COAT", lot.GetProperty("nextOperation").GetString());
        Assert.Equal(JsonValueKind.Null, lot.GetProperty("currentOperation").ValueKind);
        Assert.Equal(JsonValueKind.Null, lot.GetProperty("workOrderNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, lot.GetProperty("currentCarrier").ValueKind);

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/lots/{lotId}", Ct);
        Assert.Equal(lotId, fetched.GetProperty("lotId").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Register_non_positive_qty_is_invalid_quantity_and_writes_nothing(int qty)
    {
        var client = await api.ClientAsAsync("planner");
        var before = await client.GetFromJsonAsync<JsonElement>("/api/lots", Ct);

        var response = await client.PostAsJsonAsync("/api/lots/materials", new { materialCode = "NCM811", qty }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("INVALID_QUANTITY", (await ReadAsync(response)).GetProperty("errorCode").GetString());
        var after = await client.GetFromJsonAsync<JsonElement>("/api/lots", Ct);
        Assert.Equal(before.GetArrayLength(), after.GetArrayLength());
    }

    [Fact]
    public async Task Register_qty_beyond_column_precision_is_invalid_quantity()
    {
        var client = await api.ClientAsAsync("planner");

        var response = await client.PostAsJsonAsync(
            "/api/lots/materials", new { materialCode = "NCM811", qty = 1_000_000_000m }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("INVALID_QUANTITY", (await ReadAsync(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Register_qty_with_more_than_three_decimals_is_invalid_quantity()
    {
        var client = await api.ClientAsAsync("planner");

        var response = await client.PostAsJsonAsync(
            "/api/lots/materials", new { materialCode = "NCM811", qty = 1.0005m }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("INVALID_QUANTITY", (await ReadAsync(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Operator_cannot_register_material()
    {
        var client = await api.ClientAsAsync("operator");

        var response = await client.PostAsJsonAsync("/api/lots/materials", new { materialCode = "AL-FOIL", qty = 100 }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_material_is_404_material_not_found()
    {
        var client = await api.ClientAsAsync("planner");

        var response = await client.PostAsJsonAsync("/api/lots/materials", new { materialCode = "NOPE", qty = 100 }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("MATERIAL_NOT_FOUND", (await ReadAsync(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Registration_writes_register_event_by_user()
    {
        var client = await api.ClientAsAsync("planner");
        var created = await ReadAsync(
            await client.PostAsJsonAsync("/api/lots/materials", new { materialCode = "NCM811", qty = 250.5 }, Ct));
        var lotId = created.GetProperty("lotId").GetString();

        var events = await client.GetFromJsonAsync<JsonElement>($"/api/lots/{lotId}/events", Ct);

        var registered = Assert.Single(events.EnumerateArray());
        Assert.Equal("REGISTER", registered.GetProperty("type").GetString());
        Assert.Equal("planner", registered.GetProperty("user").GetString());
        Assert.Equal(250.5m, registered.GetProperty("qty").GetDecimal());
        Assert.Equal(JsonValueKind.Null, registered.GetProperty("operation").ValueKind);
    }

    [Fact]
    public async Task Seed_creates_nine_material_lots_filterable_by_type()
    {
        var client = await api.ClientAsAsync("operator");

        var all = await client.GetFromJsonAsync<JsonElement>("/api/lots", Ct);
        var foil = await client.GetFromJsonAsync<JsonElement>("/api/lots?type=FOIL", Ct);
        var raw = await client.GetFromJsonAsync<JsonElement>("/api/lots?type=raw", Ct);

        Assert.Equal(9, all.GetArrayLength());
        Assert.Equal(2, foil.GetArrayLength());
        Assert.Equal(7, raw.GetArrayLength());
        Assert.All(foil.EnumerateArray(), l =>
        {
            Assert.Equal(6000m, l.GetProperty("qty").GetDecimal());
            Assert.Equal("m", l.GetProperty("uom").GetString());
        });
        Assert.All(raw.EnumerateArray(), l =>
        {
            Assert.Equal(500m, l.GetProperty("qty").GetDecimal());
            Assert.Equal("MIX", l.GetProperty("nextOperation").GetString());
        });

        var firstLot = LotIds(all).First();
        var events = await client.GetFromJsonAsync<JsonElement>($"/api/lots/{firstLot}/events", Ct);
        Assert.Equal("admin", Assert.Single(events.EnumerateArray()).GetProperty("user").GetString());
    }

    [Fact]
    public async Task Search_matches_lot_id_prefix_case_insensitive()
    {
        var client = await api.ClientAsAsync("operator");

        var cathodeFoil = await client.GetFromJsonAsync<JsonElement>("/api/lots?search=fc-", Ct);
        var anyFoil = await client.GetFromJsonAsync<JsonElement>("/api/lots?search=F", Ct);

        Assert.Equal("AL-FOIL", Assert.Single(cathodeFoil.EnumerateArray()).GetProperty("materialCode").GetString());
        Assert.All(LotIds(cathodeFoil), id => Assert.StartsWith("FC-", id));
        Assert.Equal(2, anyFoil.GetArrayLength());
        Assert.All(anyFoil.EnumerateArray(), l => Assert.Equal("FOIL", l.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var client = await api.ClientAsAsync("operator");

        var lots = await client.GetFromJsonAsync<JsonElement>("/api/lots?search=%25", Ct);

        Assert.Equal(0, lots.GetArrayLength());
    }

    [Fact]
    public async Task Status_accepts_comma_list_and_filters_by_material_and_next_operation()
    {
        var client = await api.ClientAsAsync("operator");

        var waitOrRun = await client.GetFromJsonAsync<JsonElement>("/api/lots?status=WAIT,run", Ct);
        var consumed = await client.GetFromJsonAsync<JsonElement>("/api/lots?status=CONSUMED", Ct);
        var byMaterial = await client.GetFromJsonAsync<JsonElement>("/api/lots?material=al-foil", Ct);
        var coat = await client.GetFromJsonAsync<JsonElement>("/api/lots?nextOperation=COAT", Ct);
        var byWorkOrder = await client.GetFromJsonAsync<JsonElement>("/api/lots?workOrder=WO-000000-000", Ct);

        Assert.Equal(9, waitOrRun.GetArrayLength());
        Assert.Equal(0, consumed.GetArrayLength());
        Assert.Equal("AL-FOIL", Assert.Single(byMaterial.EnumerateArray()).GetProperty("materialCode").GetString());
        Assert.Equal(2, coat.GetArrayLength());
        Assert.Equal(0, byWorkOrder.GetArrayLength());
    }

    [Theory]
    [InlineData("/api/lots?status=WAIT,NOPE")]
    [InlineData("/api/lots?type=NOPE")]
    [InlineData("/api/lots?nextOperation=NOPE")]
    public async Task Unknown_filter_value_returns_400(string url)
    {
        var client = await api.ClientAsAsync("operator");

        var response = await client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lots_are_listed_newest_first()
    {
        var client = await api.ClientAsAsync("planner");
        var created = await ReadAsync(
            await client.PostAsJsonAsync("/api/lots/materials", new { materialCode = "CU-FOIL", qty = 10 }, Ct));

        var lots = await client.GetFromJsonAsync<JsonElement>("/api/lots", Ct);

        Assert.Equal(10, lots.GetArrayLength());
        Assert.Equal(created.GetProperty("lotId").GetString(), LotIds(lots).First());
    }

    [Fact]
    public async Task Unknown_lot_is_404_lot_not_found()
    {
        var client = await api.ClientAsAsync("operator");

        var lot = await client.GetAsync("/api/lots/XX-000000-000", Ct);
        var events = await client.GetAsync("/api/lots/XX-000000-000/events", Ct);

        Assert.Equal(HttpStatusCode.NotFound, lot.StatusCode);
        Assert.Equal("LOT_NOT_FOUND", (await ReadAsync(lot)).GetProperty("errorCode").GetString());
        Assert.Equal(HttpStatusCode.NotFound, events.StatusCode);
    }

    [Theory]
    [InlineData("/api/lots")]
    [InlineData("/api/lots/FC-000000-001")]
    [InlineData("/api/lots/FC-000000-001/events")]
    public async Task Lot_queries_require_authentication(string url)
    {
        var response = await api.CreateClient().GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Loaded_carrier_shows_on_lot_and_lot_shows_on_carrier()
    {
        var client = await api.ClientAsAsync("planner");
        var created = await ReadAsync(
            await client.PostAsJsonAsync("/api/lots/materials", new { materialCode = "AL-FOIL", qty = 100 }, Ct));
        var lotId = created.GetProperty("lotId").GetString()!;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            // Carrier loading arrives with the execution module; link the pair directly for the join.
            var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
            await db.Database.ExecuteSqlAsync($"""
                UPDATE carrier.carrier SET current_lot_id = l.id, status = 'Full'
                FROM lot.lot l WHERE l.lot_id = {lotId} AND carrier.code = 'BB-0001'
                """, Ct);
            await db.Database.ExecuteSqlAsync($"""
                UPDATE lot.lot SET current_carrier_id = c.id
                FROM carrier.carrier c WHERE lot.lot_id = {lotId} AND c.code = 'BB-0001'
                """, Ct);
        }

        var carrier = await client.GetFromJsonAsync<JsonElement>("/api/carriers/BB-0001", Ct);
        var lot = await client.GetFromJsonAsync<JsonElement>($"/api/lots/{lotId}", Ct);
        var byCarrier = await client.GetFromJsonAsync<JsonElement>("/api/lots?search=bb-00", Ct);

        Assert.Equal(lotId, carrier.GetProperty("lotId").GetString());
        Assert.Equal("BB-0001", lot.GetProperty("currentCarrier").GetString());
        Assert.Equal(lotId, Assert.Single(byCarrier.EnumerateArray()).GetProperty("lotId").GetString());
    }
}
