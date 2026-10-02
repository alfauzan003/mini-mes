using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiniMes.IntegrationTests.MasterData;

[Collection("api")]
public class MasterDataTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeds_eight_equipment_with_8_lane_slitters()
    {
        var client = await api.ClientAsAsync("planner");

        var equipment = await client.GetFromJsonAsync<JsonElement>("/api/equipment", Ct);

        Assert.Equal(8, equipment.GetArrayLength());
        Assert.All(equipment.EnumerateArray(), e => Assert.Equal("IDLE", e.GetProperty("status").GetString()));
        var sl01 = await client.GetFromJsonAsync<JsonElement>("/api/equipment/SL01", Ct);
        Assert.Equal(8, sl01.GetProperty("laneCount").GetInt32());
        Assert.Equal("SLIT", sl01.GetProperty("operation").GetString());
    }

    [Fact]
    public async Task Equipment_can_be_filtered_by_operation_case_insensitively()
    {
        var client = await api.ClientAsAsync("operator");

        var coaters = await client.GetFromJsonAsync<JsonElement>("/api/equipment?operation=COAT", Ct);
        var lower = await client.GetFromJsonAsync<JsonElement>("/api/equipment?operation=coat", Ct);

        Assert.Equal(["CT01", "CT02"], coaters.EnumerateArray().Select(e => e.GetProperty("code").GetString()));
        Assert.Equal(2, lower.GetArrayLength());
    }

    [Fact]
    public async Task Unknown_filter_value_returns_400()
    {
        var client = await api.ClientAsAsync("operator");

        var response = await client.GetAsync("/api/equipment?operation=NOPE", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Equipment_by_code_returns_404_equipment_not_found_for_unknown()
    {
        var client = await api.ClientAsAsync("operator");

        var response = await client.GetAsync("/api/equipment/ZZ99", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("EQUIPMENT_NOT_FOUND", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Seeds_40_empty_bobbins()
    {
        var client = await api.ClientAsAsync("operator");

        var bobbins = await client.GetFromJsonAsync<JsonElement>("/api/carriers?type=BB&status=EMPTY", Ct);
        var pancakes = await client.GetFromJsonAsync<JsonElement>("/api/carriers?type=PC", Ct);

        Assert.Equal(40, bobbins.GetArrayLength());
        Assert.Equal(200, pancakes.GetArrayLength());
        Assert.All(bobbins.EnumerateArray(), c =>
        {
            Assert.Equal("BB", c.GetProperty("type").GetString());
            Assert.Equal("EMPTY", c.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, c.GetProperty("lotId").ValueKind);
        });
    }

    [Fact]
    public async Task Carrier_by_code_returns_the_carrier()
    {
        var client = await api.ClientAsAsync("operator");

        var carrier = await client.GetFromJsonAsync<JsonElement>("/api/carriers/BB-0001", Ct);

        Assert.Equal("BB-0001", carrier.GetProperty("code").GetString());
        Assert.Equal("BB", carrier.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Carrier_by_code_returns_404_carrier_not_found_for_unknown()
    {
        var client = await api.ClientAsAsync("operator");

        var response = await client.GetAsync("/api/carriers/BB-9999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("CARRIER_NOT_FOUND", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Products_have_four_step_route()
    {
        var client = await api.ClientAsAsync("planner");

        var products = await client.GetFromJsonAsync<JsonElement>("/api/products", Ct);

        Assert.Equal(
            ["ANOD-GRAPHITE", "CATH-NCM811"],
            products.EnumerateArray().Select(p => p.GetProperty("code").GetString()));
        foreach (var product in products.EnumerateArray())
        {
            var route = product.GetProperty("route").EnumerateArray().ToList();
            Assert.Equal(["MIX", "COAT", "CAL", "SLIT"], route.Select(s => s.GetProperty("operation").GetString()));
            Assert.Equal([10, 20, 30, 40], route.Select(s => s.GetProperty("seq").GetInt32()));
            Assert.Equal(["kg", "m", "m", "m"], route.Select(s => s.GetProperty("uom").GetString()));
        }
    }

    [Fact]
    public async Task Materials_cover_both_polarities_with_raw_and_foil()
    {
        var client = await api.ClientAsAsync("planner");

        var materials = await client.GetFromJsonAsync<JsonElement>("/api/materials", Ct);

        Assert.Equal(9, materials.GetArrayLength());
        var foil = materials.EnumerateArray().Single(m => m.GetProperty("code").GetString() == "AL-FOIL");
        Assert.Equal("FOIL", foil.GetProperty("kind").GetString());
        Assert.Equal("CATHODE", foil.GetProperty("polarity").GetString());
        Assert.Equal("m", foil.GetProperty("uom").GetString());
        Assert.Equal("Aluminium foil 15 µm", foil.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("/api/equipment")]
    [InlineData("/api/equipment/SL01")]
    [InlineData("/api/carriers")]
    [InlineData("/api/carriers/BB-0001")]
    [InlineData("/api/products")]
    [InlineData("/api/materials")]
    public async Task Master_data_requires_authentication(string url)
    {
        var response = await api.CreateClient().GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
