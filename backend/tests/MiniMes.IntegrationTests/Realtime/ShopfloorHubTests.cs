using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Realtime;

namespace MiniMes.IntegrationTests.Realtime;

[Collection("api")]
public class ShopfloorHubTests(MesApiFactory api) : IAsyncLifetime
{
    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Authenticated_client_receives_equipment_status_changes()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync("NCM811");
        await using var connection = await api.ConnectShopfloorAsync("planner");
        var received = new TaskCompletionSource<EquipmentStatusEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<EquipmentStatusEvent>(RealtimeMethods.EquipmentStatusChanged, e =>
        {
            if (e.Code == "MX01")
            {
                received.TrySetResult(e);
            }
        });

        await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);

        var change = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal(EquipmentStatus.Running, change.Status);
    }

    [Fact]
    public async Task Anonymous_connection_is_rejected()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => api.ConnectShopfloorAsync(null));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task Access_token_in_query_string_is_accepted_only_for_hubs()
    {
        var token = await api.LoginTokenAsync("planner");
        var client = api.CreateClient();

        var hub = await client.PostAsync(
            $"/hubs/shopfloor/negotiate?negotiateVersion=1&access_token={token}", null, Ct);
        var hubAnonymous = await client.PostAsync("/hubs/shopfloor/negotiate?negotiateVersion=1", null, Ct);
        var nonHub = await client.GetAsync($"/api/auth/me?access_token={token}", Ct);

        Assert.Equal(HttpStatusCode.OK, hub.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, hubAnonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, nonHub.StatusCode);
    }
}
