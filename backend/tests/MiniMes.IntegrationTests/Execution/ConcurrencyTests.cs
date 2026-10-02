using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.IntegrationTests.Execution;

[Collection("api")]
public class ConcurrencyTests(MesApiFactory api) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_track_in_of_same_raw_lot_lets_exactly_one_win()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await api.ResetDatabaseAsync();
            var driver = new ProductionDriver(api);
            var first = await driver.CreateReleasedWorkOrderAsync(mix: "MX01");
            var second = await driver.CreateReleasedWorkOrderAsync(mix: "MX02");
            var raw = (await driver.MaterialLotsAsync("NCM811")).Single();

            var responses = await Task.WhenAll(
                driver.TrackInAsync("MX01", first, OperationCode.Mix, raw),
                driver.TrackInAsync("MX02", second, OperationCode.Mix, raw));

            var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var loser = responses.Single(r => r != winner);
            var body = await loser.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var code = body.GetProperty("errorCode").GetString();
            Assert.True(
                (loser.StatusCode == HttpStatusCode.Conflict && code == "CONCURRENCY_CONFLICT")
                    || (loser.StatusCode == HttpStatusCode.UnprocessableEntity && code == "LOT_NOT_AVAILABLE"),
                $"Unexpected loser response {(int)loser.StatusCode} {code}.");

            Assert.Equal(LotStatus.Run, (await driver.LotAsync(raw)).Status);
            var client = await driver.OperatorAsync();
            var open = await client.GetFromJsonAsync<JsonElement>("/api/runs?open=true", Ct);
            var run = Assert.Single(open.EnumerateArray());
            Assert.Equal(raw, Assert.Single(run.GetProperty("inputs").EnumerateArray()).GetProperty("lotId").GetString());
        }
    }
}
