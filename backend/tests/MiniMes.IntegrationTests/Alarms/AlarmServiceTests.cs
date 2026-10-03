using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Modules.Alarms;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Features.TrackOut;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using AlarmCodeEntity = MiniMes.Api.Modules.Alarms.Domain.AlarmCode;

namespace MiniMes.IntegrationTests.Alarms;

[Collection("api")]
public class AlarmServiceTests(MesApiFactory api) : IAsyncLifetime
{
    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> WithServiceAsync<T>(Func<AlarmService, Task<T>> action)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AlarmService>());
    }

    private async Task<Guid> RaiseOkAsync(string equipment, string code)
    {
        var result = await WithServiceAsync(s => s.RaiseAsync(equipment, code, Ct));
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value;
    }

    private async Task ClearOkAsync(string equipment, string code)
    {
        var result = await WithServiceAsync(s => s.ClearAsync(equipment, code, Ct));
        Assert.True(result.IsSuccess, result.Error?.Code);
    }

    private async Task<EquipmentStatus> StatusAsync(string code) => (await _driver.EquipmentAsync(code)).Status;

    private async Task<int> AlarmCountAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        return await db.Set<Alarm>().CountAsync(Ct);
    }

    [Fact]
    public async Task Critical_alarm_takes_idle_equipment_down_and_clear_recovers_idle()
    {
        await RaiseOkAsync("MX01", "MX-AGITATOR-FAULT");
        Assert.Equal(EquipmentStatus.Down, await StatusAsync("MX01"));

        await ClearOkAsync("MX01", "MX-AGITATOR-FAULT");

        Assert.Equal(EquipmentStatus.Idle, await StatusAsync("MX01"));
    }

    [Fact]
    public async Task Raising_an_active_alarm_again_does_not_duplicate()
    {
        var first = await RaiseOkAsync("MX01", "MX-TEMP-HIGH");
        var second = await RaiseOkAsync("MX01", "MX-TEMP-HIGH");

        Assert.Equal(first, second);
        Assert.Equal(1, await AlarmCountAsync());
    }

    [Fact]
    public async Task Concurrent_raises_of_the_same_alarm_return_one_alarm()
    {
        var ids = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => RaiseOkAsync("MX01", "MX-AGITATOR-FAULT")));

        Assert.Single(ids.Distinct());
        Assert.Equal(1, await AlarmCountAsync());
        Assert.Equal(EquipmentStatus.Down, await StatusAsync("MX01"));
    }

    [Fact]
    public async Task Warning_does_not_change_status()
    {
        await RaiseOkAsync("MX01", "MX-VAC-LOW");

        Assert.Equal(EquipmentStatus.Idle, await StatusAsync("MX01"));
    }

    [Fact]
    public async Task Unknown_equipment_is_not_found()
    {
        var result = await WithServiceAsync(s => s.RaiseAsync("NOPE", "MX-VAC-LOW", Ct));

        Assert.Equal("EQUIPMENT_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task Alarm_code_of_another_operation_is_rejected()
    {
        var result = await WithServiceAsync(s => s.RaiseAsync("MX01", "CT-WEB-BREAK", Ct));

        Assert.Equal("ALARM_CODE_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task Clearing_an_alarm_that_is_not_active_is_a_no_op()
    {
        var result = await WithServiceAsync(s => s.ClearAsync("MX01", "MX-VAC-LOW", Ct));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Critical_during_run_then_clear_returns_running()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync("NCM811");
        await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);

        await RaiseOkAsync("MX01", "MX-AGITATOR-FAULT");
        Assert.Equal(EquipmentStatus.Down, await StatusAsync("MX01"));

        await ClearOkAsync("MX01", "MX-AGITATOR-FAULT");
        Assert.Equal(EquipmentStatus.Running, await StatusAsync("MX01"));
    }

    [Fact]
    public async Task Track_out_while_down_stays_down_then_clear_gives_idle()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");
        var foil = (await _driver.MaterialLotsAsync("AL-FOIL")).Single();
        var mix = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);
        var slurry = (await _driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m))).Outputs.Single().LotId!;
        await _driver.TrackOutOkAsync(mix.Id, [.. raws.Select(raw => new Consumption(raw, 100m))]);
        var coat = await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        await _driver.ProduceOkAsync(coat.Id, new OutputLine("BB-0001", null, 1200m, 20m));

        await RaiseOkAsync("CT01", "CT-WEB-BREAK");
        await _driver.TrackOutOkAsync(coat.Id, new Consumption(foil, 1300m));
        Assert.Equal(EquipmentStatus.Down, await StatusAsync("CT01"));

        await ClearOkAsync("CT01", "CT-WEB-BREAK");
        Assert.Equal(EquipmentStatus.Idle, await StatusAsync("CT01"));
    }

    [Fact]
    public async Task Track_in_while_down_is_not_available()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync("NCM811");
        await RaiseOkAsync("MX01", "MX-AGITATOR-FAULT");

        var response = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, raws);

        await ProductionDriver.AssertErrorAsync(response, 422, "EQUIPMENT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Equipment_stays_down_until_last_critical_clears()
    {
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
            db.Set<AlarmCodeEntity>().Add(
                new AlarmCodeEntity("CT-WEB-BREAK-2", "Second critical", AlarmSeverity.Critical, OperationCode.Coat));
            await db.SaveChangesAsync(Ct);
        }

        await RaiseOkAsync("CT01", "CT-WEB-BREAK");
        await RaiseOkAsync("CT01", "CT-WEB-BREAK-2");

        await ClearOkAsync("CT01", "CT-WEB-BREAK");
        Assert.Equal(EquipmentStatus.Down, await StatusAsync("CT01"));

        await ClearOkAsync("CT01", "CT-WEB-BREAK-2");
        Assert.Equal(EquipmentStatus.Idle, await StatusAsync("CT01"));
    }

    [Fact]
    public async Task Critical_in_maintenance_keeps_maintenance()
    {
        var admin = await api.ClientAsAsync("admin");
        (await admin.PostAsync("/api/equipment/MX01/maintenance/start", null, Ct)).EnsureSuccessStatusCode();

        await RaiseOkAsync("MX01", "MX-AGITATOR-FAULT");
        Assert.Equal(EquipmentStatus.Maintenance, await StatusAsync("MX01"));

        await ClearOkAsync("MX01", "MX-AGITATOR-FAULT");
        Assert.Equal(EquipmentStatus.Maintenance, await StatusAsync("MX01"));
    }
}
