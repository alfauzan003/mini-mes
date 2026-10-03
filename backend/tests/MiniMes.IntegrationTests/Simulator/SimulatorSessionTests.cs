using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MiniMes.Api.Modules.Alarms;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Features.TrackOut;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Simulator;
using OperationCode = MiniMes.Api.Modules.WorkOrders.Domain.OperationCode;
using EquipmentStatus = MiniMes.Api.Modules.Equipment.Domain.EquipmentStatus;

namespace MiniMes.IntegrationTests.Simulator;

[Collection("api")]
public class SimulatorSessionTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private ProductionDriver _driver = null!;
    private HubConnection _connection = null!;
    private SimulatorSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);

        var options = new SimulatorOptions
        {
            HubUrl = new Uri(api.Server.BaseAddress, "/hubs/machine").ToString(),
            ApiKey = MesApiFactory.MachineKey,
            DriftChancePerTick = 0
        };
        _connection = SimulatorConnection.Build(options, o =>
        {
            o.Transports = HttpTransportType.LongPolling;
            o.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
        });
        _session = new SimulatorSession(
            _connection, options, _time, new Random(7), NullLogger<SimulatorSession>.Instance);
        await _connection.StartAsync(Ct);
        await _session.LoadAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<bool> PollAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;
            await Task.Delay(25, Ct);
        }

        return false;
    }

    [Fact]
    public async Task Session_reports_running_speed_after_track_in()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");
        var foil = (await _driver.MaterialLotsAsync("AL-FOIL")).Single();
        var mix = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);
        var slurry = (await _driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m)))
            .Outputs.Single().LotId!;
        await _driver.TrackOutOkAsync(mix.Id, [.. raws.Select(r => new Consumption(r, 100m))]);

        await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        Assert.True(await PollAsync(() => Task.FromResult(_session.StatusOf("CT01") == MiniMes.Simulator.EquipmentStatus.Running)));
        for (var i = 0; i < 5; i++)
            await _session.TickAsync(Ct);

        var client = await api.ClientAsAsync("operator");
        var latest = await client.GetFromJsonAsync<JsonElement>("/api/readings/latest", Ct);
        var speed = latest.EnumerateArray().Single(r =>
            r.GetProperty("equipmentCode").GetString() == "CT01" && r.GetProperty("parameter").GetString() == "Line speed");
        Assert.True(speed.GetProperty("value").GetDecimal() > 20m);
    }

    [Fact]
    public async Task Injected_fault_takes_equipment_down_and_session_clears_it()
    {
        var admin = await api.ClientAsAsync("admin");

        var response = await admin.PostAsync("/api/equipment/CT01/inject-fault", null, Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // The hub delivers the fault asynchronously; ticking is harmless until it lands.
        Assert.True(await PollAsync(async () =>
        {
            await _session.TickAsync(Ct);
            return (await _driver.EquipmentAsync("CT01")).Status == EquipmentStatus.Down;
        }));

        _time.Advance(TimeSpan.FromSeconds(61));
        await _session.TickAsync(Ct);

        Assert.Equal(EquipmentStatus.Idle, (await _driver.EquipmentAsync("CT01")).Status);
    }

    [Fact]
    public async Task Reload_clears_alarms_left_active_on_server()
    {
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var raised = await scope.ServiceProvider.GetRequiredService<AlarmService>()
                .RaiseAsync("CT01", "CT-TEMP-HIGH", Ct);
            Assert.True(raised.IsSuccess, raised.Error?.Code);
        }

        await _session.LoadAsync(Ct);
        await _session.TickAsync(Ct);

        var admin = await api.ClientAsAsync("admin");
        var active = await admin.GetFromJsonAsync<JsonElement>("/api/alarms?active=true", Ct);
        Assert.Empty(active.EnumerateArray());
    }
}
