using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Machine;
using MiniMes.Api.Modules.Equipment.Parameters;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Realtime;

namespace MiniMes.IntegrationTests.Equipment;

[Collection("api")]
public class MachineHubTests(MesApiFactory api) : IAsyncLifetime
{
    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TimeSpan Wait => TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Wrong_key_is_rejected()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => api.ConnectMachineAsync("wrong-key-wrong-key-123"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task User_token_is_not_a_machine_key()
    {
        var token = await api.LoginTokenAsync("admin");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => api.ConnectMachineAsync(token));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task Equipment_states_include_parameters_and_alarm_codes()
    {
        await using var machine = await api.ConnectMachineAsync();

        var states = await machine.InvokeAsync<List<MachineEquipmentState>>("GetEquipmentStates", Ct);

        Assert.Equal(8, states.Count);
        var coater = states.Single(s => s.Code == "CT01");
        Assert.Equal(OperationCode.Coat, coater.Operation);
        Assert.Equal(3, coater.Parameters.Count);
        Assert.Equal(3, coater.AlarmCodes.Count);
        Assert.Empty(coater.ActiveAlarmCodes);
        Assert.Contains(coater.Parameters, p => p is { Name: "Dryer temp", HighAlarmCode: "CT-TEMP-HIGH", Setpoint: 130m });
    }

    [Fact]
    public async Task Reported_readings_appear_in_latest_and_reach_shopfloor_clients()
    {
        await using var machine = await api.ConnectMachineAsync();
        await using var shopfloor = await api.ConnectShopfloorAsync("operator");
        var received = new TaskCompletionSource<LiveReadingDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        shopfloor.On<LiveReadingDto>(RealtimeMethods.ParameterReading, r => received.TrySetResult(r));

        await machine.InvokeAsync("ReportReadings", "CT01", new[] { new ReadingInput("Dryer temp", 131.5m) }, Ct);

        var live = await received.Task.WaitAsync(Wait, Ct);
        Assert.Equal("CT01", live.EquipmentCode);
        Assert.Equal(131.5m, live.Value);
        var client = await api.ClientAsAsync("operator");
        var latest = await client.GetFromJsonAsync<JsonElement>("/api/readings/latest", Ct);
        Assert.Contains(latest.EnumerateArray(), r =>
            r.GetProperty("equipmentCode").GetString() == "CT01"
            && r.GetProperty("parameter").GetString() == "Dryer temp"
            && r.GetProperty("value").GetDecimal() == 131.5m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Reading_without_parameter_is_a_hub_exception(string? parameter)
    {
        await using var machine = await api.ConnectMachineAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => machine.InvokeAsync(
            "ReportReadings", "CT01", new[] { new ReadingInput(parameter!, 1m) }, Ct));

        Assert.Contains("INVALID_INPUT:", ex.Message);
    }

    [Fact]
    public async Task Null_reading_entry_and_blank_codes_are_hub_exceptions()
    {
        await using var machine = await api.ConnectMachineAsync();

        var nullEntry = await Assert.ThrowsAsync<HubException>(() => machine.InvokeAsync(
            "ReportReadings", "CT01", new ReadingInput?[] { null }, Ct));
        var noReadings = await Assert.ThrowsAsync<HubException>(() => machine.InvokeAsync(
            "ReportReadings", "CT01", (ReadingInput[]?)null, Ct));
        var noEquipment = await Assert.ThrowsAsync<HubException>(() => machine.InvokeAsync(
            "RaiseAlarm", "", "CT-WEB-BREAK", Ct));
        var noAlarm = await Assert.ThrowsAsync<HubException>(() => machine.InvokeAsync(
            "ClearAlarm", "CT01", (string?)null, Ct));

        Assert.All(new[] { nullEntry, noReadings, noEquipment, noAlarm }, e => Assert.Contains("INVALID_INPUT:", e.Message));
    }

    [Fact]
    public async Task Unknown_parameter_throws_hub_exception_with_code()
    {
        await using var machine = await api.ConnectMachineAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => machine.InvokeAsync(
            "ReportReadings", "CT01", new[] { new ReadingInput("Nope", 1m) }, Ct));

        Assert.Contains("UNKNOWN_PARAMETER:", ex.Message);
    }

    [Fact]
    public async Task Raise_critical_alarm_takes_equipment_down()
    {
        await using var machine = await api.ConnectMachineAsync();

        await machine.InvokeAsync("RaiseAlarm", "CT01", "CT-WEB-BREAK", Ct);

        Assert.Equal(EquipmentStatus.Down, (await _driver.EquipmentAsync("CT01")).Status);
        var states = await machine.InvokeAsync<List<MachineEquipmentState>>("GetEquipmentStates", Ct);
        Assert.Equal(["CT-WEB-BREAK"], states.Single(s => s.Code == "CT01").ActiveAlarmCodes);
    }

    [Fact]
    public async Task Clear_alarm_recovers()
    {
        await using var machine = await api.ConnectMachineAsync();
        await machine.InvokeAsync("RaiseAlarm", "CT01", "CT-WEB-BREAK", Ct);

        await machine.InvokeAsync("ClearAlarm", "CT01", "CT-WEB-BREAK", Ct);

        Assert.Equal(EquipmentStatus.Idle, (await _driver.EquipmentAsync("CT01")).Status);
    }

    [Fact]
    public async Task Unknown_alarm_code_throws_hub_exception_with_code()
    {
        await using var machine = await api.ConnectMachineAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => machine.InvokeAsync("RaiseAlarm", "CT01", "NOPE", Ct));

        Assert.Contains("ALARM_CODE_NOT_FOUND:", ex.Message);
    }

    [Fact]
    public async Task Inject_fault_reaches_connected_simulator()
    {
        await using var machine = await api.ConnectMachineAsync();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        machine.On<string>("InjectFault", code => received.TrySetResult(code));
        var admin = await api.ClientAsAsync("admin");

        var response = await admin.PostAsync("/api/equipment/CT01/inject-fault", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("CT01", await received.Task.WaitAsync(Wait, Ct));
    }

    [Fact]
    public async Task Inject_fault_for_unknown_equipment_is_404()
    {
        await using var machine = await api.ConnectMachineAsync();
        var admin = await api.ClientAsAsync("admin");

        var response = await admin.PostAsync("/api/equipment/NOPE/inject-fault", null, Ct);

        await ProductionDriver.AssertErrorAsync(response, 404, "EQUIPMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Inject_fault_without_simulator_is_409_simulator_offline()
    {
        var admin = await api.ClientAsAsync("admin");

        var response = await admin.PostAsync("/api/equipment/CT01/inject-fault", null, Ct);

        await ProductionDriver.AssertErrorAsync(response, 409, "SIMULATOR_OFFLINE");
    }

    [Fact]
    public async Task Simulator_presence_drops_when_the_simulator_disconnects()
    {
        var machine = await api.ConnectMachineAsync();
        await machine.DisposeAsync();
        var admin = await api.ClientAsAsync("admin");

        var deadline = DateTime.UtcNow + Wait;
        HttpResponseMessage response;
        do
        {
            response = await admin.PostAsync("/api/equipment/CT01/inject-fault", null, Ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                break;
            }

            await Task.Delay(50, Ct);
        }
        while (DateTime.UtcNow < deadline);

        await ProductionDriver.AssertErrorAsync(response, 409, "SIMULATOR_OFFLINE");
    }

    [Fact]
    public async Task Operator_cannot_inject_fault()
    {
        await using var machine = await api.ConnectMachineAsync();
        var client = await api.ClientAsAsync("operator");

        var response = await client.PostAsync("/api/equipment/CT01/inject-fault", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Status_change_is_pushed_to_simulator()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync("NCM811");
        await using var machine = await api.ConnectMachineAsync();
        var received = new TaskCompletionSource<(string Code, EquipmentStatus Status)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        machine.On<string, EquipmentStatus>("EquipmentStateChanged", (code, status) =>
        {
            if (code == "MX01")
            {
                received.TrySetResult((code, status));
            }
        });

        await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);

        var change = await received.Task.WaitAsync(Wait, Ct);
        Assert.Equal(EquipmentStatus.Running, change.Status);
    }
}
