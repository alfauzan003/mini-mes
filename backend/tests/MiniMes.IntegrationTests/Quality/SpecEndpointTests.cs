using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MiniMes.IntegrationTests.Quality;

[Collection("api")]
public class SpecEndpointTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        await api.SeedInspectionSpecsAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> CoatSpecAsync(HttpClient client)
    {
        var specs = await client.GetFromJsonAsync<JsonElement>("/api/specs?product=CATH-NCM811&operation=COAT", Ct);
        return specs.EnumerateArray().Single();
    }

    [Fact]
    public async Task Lists_cathode_coat_spec()
    {
        var client = await api.ClientAsAsync("operator");

        var specs = await client.GetFromJsonAsync<JsonElement>("/api/specs?product=CATH-NCM811&operation=COAT", Ct);

        var spec = Assert.Single(specs.EnumerateArray());
        Assert.Equal("CATH-NCM811", spec.GetProperty("productCode").GetString());
        Assert.Equal("COAT", spec.GetProperty("operation").GetString());
        Assert.Equal("Loading weight", spec.GetProperty("itemName").GetString());
        Assert.Equal("mg/cm²", spec.GetProperty("unit").GetString());
        Assert.Equal(19.5m, spec.GetProperty("lsl").GetDecimal());
        Assert.Equal(20.5m, spec.GetProperty("usl").GetDecimal());
    }

    [Fact]
    public async Task Lists_all_specs_in_product_operation_seq_order()
    {
        var client = await api.ClientAsAsync("operator");

        var specs = await client.GetFromJsonAsync<JsonElement>("/api/specs", Ct);

        Assert.Equal(14, specs.GetArrayLength());
        Assert.Equal("ANOD-GRAPHITE", specs[0].GetProperty("productCode").GetString());
    }

    [Fact]
    public async Task Unknown_operation_filter_returns_400()
    {
        var client = await api.ClientAsAsync("operator");

        var response = await client.GetAsync("/api/specs?operation=NOPE", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Qc_updates_limits()
    {
        var qc = await api.ClientAsAsync("qc");
        var spec = await CoatSpecAsync(qc);

        var response = await qc.PutAsJsonAsync(
            $"/api/specs/{spec.GetProperty("id").GetGuid()}", new { lsl = 19.0m, usl = 21.0m }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(19.0m, updated.GetProperty("lsl").GetDecimal());
        Assert.Equal(21.0m, updated.GetProperty("usl").GetDecimal());
        var reread = await CoatSpecAsync(qc);
        Assert.Equal(21.0m, reread.GetProperty("usl").GetDecimal());
    }

    [Fact]
    public async Task Operator_cannot_update_limits()
    {
        var op = await api.ClientAsAsync("operator");
        var spec = await CoatSpecAsync(op);

        var response = await op.PutAsJsonAsync(
            $"/api/specs/{spec.GetProperty("id").GetGuid()}", new { lsl = 19.0m, usl = 21.0m }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Inverted_limits_are_invalid_spec_limits()
    {
        var qc = await api.ClientAsAsync("qc");
        var spec = await CoatSpecAsync(qc);

        var response = await qc.PutAsJsonAsync(
            $"/api/specs/{spec.GetProperty("id").GetGuid()}", new { lsl = 21.0m, usl = 19.0m }, Ct);

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_SPEC_LIMITS");
    }

    [Theory]
    [InlineData("100000000", "100000001")]
    [InlineData("-100000000", "1")]
    [InlineData("1", "2.12345")]
    public async Task Limits_outside_numeric_12_4_are_invalid_spec_limits(string lsl, string usl)
    {
        var qc = await api.ClientAsAsync("qc");
        var spec = await CoatSpecAsync(qc);
        var body = "{\"lsl\": " + lsl + ", \"usl\": " + usl + "}";

        var response = await qc.PutAsync(
            $"/api/specs/{spec.GetProperty("id").GetGuid()}",
            new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        await ProductionDriver.AssertErrorAsync(response, 422, "INVALID_SPEC_LIMITS");
        Assert.Equal(20.5m, (await CoatSpecAsync(qc)).GetProperty("usl").GetDecimal());
    }

    [Fact]
    public async Task Max_numeric_12_4_limits_are_accepted()
    {
        var qc = await api.ClientAsAsync("qc");
        var spec = await CoatSpecAsync(qc);

        var response = await qc.PutAsJsonAsync(
            $"/api/specs/{spec.GetProperty("id").GetGuid()}", new { lsl = -99999999.9999m, usl = 99999999.9999m }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_spec_is_404()
    {
        var qc = await api.ClientAsAsync("qc");

        var response = await qc.PutAsJsonAsync($"/api/specs/{Guid.NewGuid()}", new { lsl = 1m, usl = 2m }, Ct);

        await ProductionDriver.AssertErrorAsync(response, 404, "SPEC_NOT_FOUND");
    }

    [Fact]
    public async Task Defect_codes_for_coat_include_general()
    {
        var client = await api.ClientAsAsync("operator");

        var codes = await client.GetFromJsonAsync<JsonElement>("/api/defect-codes?operation=COAT", Ct);

        var names = codes.EnumerateArray().Select(c => c.GetProperty("code").GetString()).ToList();
        Assert.Contains("CT-PINHOLE", names);
        Assert.Contains("GEN-OTHER", names);
        Assert.DoesNotContain("SL-BURR", names);
    }

    [Fact]
    public async Task Defect_codes_without_filter_list_all()
    {
        var client = await api.ClientAsAsync("operator");

        var codes = await client.GetFromJsonAsync<JsonElement>("/api/defect-codes", Ct);

        Assert.Equal(14, codes.GetArrayLength());
    }
}
