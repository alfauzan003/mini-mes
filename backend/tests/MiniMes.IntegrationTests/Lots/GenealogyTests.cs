using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniMes.Api.Modules.Lots.Features.Genealogy;

namespace MiniMes.IntegrationTests.Lots;

[Collection("api")]
public class GenealogyTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpResponseMessage> GetAsync(string lotId, string? direction = null) =>
        await (await _driver.OperatorAsync()).GetAsync(
            $"/api/lots/{lotId}/genealogy" + (direction is null ? "" : $"?direction={direction}"), Ct);

    private async Task<GenealogyGraph> GraphAsync(string lotId, string? direction = null)
    {
        var response = await GetAsync(lotId, direction);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GenealogyGraph>(Json, Ct))!;
    }

    [Fact]
    public async Task Backward_from_pancake_reaches_raw_materials()
    {
        var flow = await _driver.RunFullFlowAsync(target: 8);
        var pancake = flow.Pancakes[0];

        var graph = await GraphAsync(pancake, "backward");

        Assert.Equal(pancake, graph.RootLotId);
        string[] expected = [pancake, flow.Electrode, flow.Foil, flow.Slurry, .. flow.Raws];
        Assert.Equal(expected.Order(), graph.Nodes.Select(n => n.LotId).Order());
        Assert.Equal(7, graph.Edges.Count);
        Assert.Contains(new GenealogyEdge(flow.Electrode, pancake), graph.Edges);
        Assert.Contains(new GenealogyEdge(flow.Slurry, flow.Electrode), graph.Edges);
        Assert.Contains(new GenealogyEdge(flow.Foil, flow.Electrode), graph.Edges);
        Assert.All(flow.Raws, raw => Assert.Contains(new GenealogyEdge(raw, flow.Slurry), graph.Edges));
    }

    [Fact]
    public async Task Direction_defaults_to_backward_and_is_case_insensitive()
    {
        var flow = await _driver.RunFullFlowAsync(target: 8);
        var pancake = flow.Pancakes[0];

        var byDefault = await GraphAsync(pancake);
        var upper = await GraphAsync(pancake, "BACKWARD");

        Assert.Equal(8, byDefault.Nodes.Count);
        Assert.Equal(byDefault.Edges, upper.Edges);
    }

    [Fact]
    public async Task Forward_from_foil_reaches_all_pancakes()
    {
        var flow = await _driver.RunFullFlowAsync(target: 8);

        var graph = await GraphAsync(flow.Foil, "forward");

        string[] expected = [flow.Foil, flow.Electrode, .. flow.Pancakes];
        Assert.Equal(expected.Order(), graph.Nodes.Select(n => n.LotId).Order());
        Assert.Equal(9, graph.Edges.Count);
        Assert.Contains(new GenealogyEdge(flow.Foil, flow.Electrode), graph.Edges);
        Assert.All(flow.Pancakes, p => Assert.Contains(new GenealogyEdge(flow.Electrode, p), graph.Edges));
    }

    [Fact]
    public async Task Forward_from_raw_material_reaches_everything_made_from_it()
    {
        var flow = await _driver.RunFullFlowAsync(target: 8);

        var graph = await GraphAsync(flow.Raws[0], "forward");

        Assert.Equal(11, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.LotId == flow.Slurry);
        Assert.All(flow.Pancakes, p => Assert.Contains(graph.Nodes, n => n.LotId == p));
    }

    [Fact]
    public async Task Material_lot_without_children_returns_root_only()
    {
        var lot = (await _driver.MaterialLotsAsync("NCM811")).Single();

        var forward = await GraphAsync(lot, "forward");
        var backward = await GraphAsync(lot);

        Assert.Equal(lot, forward.RootLotId);
        Assert.Equal(lot, Assert.Single(forward.Nodes).LotId);
        Assert.Empty(forward.Edges);
        Assert.Equal(lot, Assert.Single(backward.Nodes).LotId);
        Assert.Empty(backward.Edges);
    }

    [Fact]
    public async Task Lot_id_is_resolved_like_a_scan()
    {
        var lot = (await _driver.MaterialLotsAsync("NCM811")).Single();

        var graph = await GraphAsync(lot.ToLowerInvariant());

        Assert.Equal(lot, graph.RootLotId);
    }

    [Fact]
    public async Task Unknown_lot_is_404()
    {
        var response = await GetAsync("NO-SUCH-LOT");

        await ProductionDriver.AssertErrorAsync(response, 404, "LOT_NOT_FOUND");
    }

    [Fact]
    public async Task Invalid_direction_is_400()
    {
        var lot = (await _driver.MaterialLotsAsync("NCM811")).Single();

        var response = await GetAsync(lot, "sideways");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
