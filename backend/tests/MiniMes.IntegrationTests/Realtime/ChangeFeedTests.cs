using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using MiniMes.Api.Modules.Alarms;
using MiniMes.Api.Modules.Alarms.Features.Queries;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Realtime;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.IntegrationTests.Realtime;

[Collection("api")]
public class ChangeFeedTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly string[] CathodeMix = ["NCM811", "PVDF", "SUPER-P", "NMP"];

    private readonly RecordingRealtimePublisher _published = new();
    private WebApplicationFactory<Program> _factory = null!;
    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _factory = api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.Replace(ServiceDescriptor.Singleton<IRealtimePublisher>(_published))));
        _driver = new ProductionDriver(_factory);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task WithAlarmServiceAsync(Func<AlarmService, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AlarmService>());
    }

    [Fact]
    public async Task Track_in_publishes_equipment_lot_and_work_order_events()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync(CathodeMix);
        _published.Clear();

        await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);

        Assert.All(_published.Events, e => Assert.Equal(RealtimeAudience.Shopfloor, e.Audience));
        Assert.Contains(
            new EquipmentStatusEvent("MX01", EquipmentStatus.Running),
            _published.Payloads<EquipmentStatusEvent>(RealtimeMethods.EquipmentStatusChanged));
        var lots = _published.Payloads<LotChangedEvent>(RealtimeMethods.LotChanged).ToList();
        foreach (var raw in raws)
        {
            Assert.Contains(lots, l => l.LotId == raw && l.Status == LotStatus.Run);
        }

        var progress = Assert.Single(_published.Payloads<WorkOrderProgressEvent>(RealtimeMethods.WorkOrderProgressed));
        Assert.Equal(wo.Id, progress.Id);
        Assert.Equal(wo.Number, progress.Number);
        Assert.Equal(WorkOrderStatus.Running, progress.Status);
        Assert.Equal(8, progress.TargetQty);
    }

    [Fact]
    public async Task Failed_command_publishes_nothing()
    {
        var wo = await _driver.CreateReleasedWorkOrderAsync();
        var raws = await _driver.MaterialLotsAsync("NCM811");
        _published.Clear();

        var response = await _driver.TrackInAsync("MX02", wo, OperationCode.Mix, raws);

        await ProductionDriver.AssertErrorAsync(response, 422, "EQUIPMENT_NOT_ASSIGNED");
        Assert.Empty(_published.Events);
    }

    [Fact]
    public async Task Finished_pancakes_publish_work_order_progress()
    {
        var flow = await _driver.RunFullFlowAsync(target: 8);

        var last = _published.Payloads<WorkOrderProgressEvent>(RealtimeMethods.WorkOrderProgressed)
            .Last(p => p.Id == flow.WorkOrder.Id);
        Assert.Equal(WorkOrderStatus.Completed, last.Status);
        Assert.Equal(8, last.GoodCount);
    }

    [Fact]
    public async Task Critical_alarm_publishes_alarm_raised_and_equipment_down()
    {
        _published.Clear();

        await WithAlarmServiceAsync(s => s.RaiseAsync("MX01", "MX-AGITATOR-FAULT", Ct));

        var raised = Assert.Single(_published.Payloads<AlarmDto>(RealtimeMethods.AlarmRaised));
        Assert.Equal("MX01", raised.EquipmentCode);
        Assert.Equal("MX-AGITATOR-FAULT", raised.Code);
        Assert.Null(raised.ClearedAt);
        Assert.Equal(
            [new EquipmentStatusEvent("MX01", EquipmentStatus.Down)],
            _published.Payloads<EquipmentStatusEvent>(RealtimeMethods.EquipmentStatusChanged));
    }

    [Fact]
    public async Task Clearing_and_acknowledging_publish_alarm_events()
    {
        await WithAlarmServiceAsync(s => s.RaiseAsync("MX01", "MX-AGITATOR-FAULT", Ct));
        _published.Clear();

        await WithAlarmServiceAsync(s => s.ClearAsync("MX01", "MX-AGITATOR-FAULT", Ct));

        var cleared = Assert.Single(_published.Payloads<AlarmDto>(RealtimeMethods.AlarmCleared));
        Assert.NotNull(cleared.ClearedAt);
        Assert.Equal(
            [new EquipmentStatusEvent("MX01", EquipmentStatus.Idle)],
            _published.Payloads<EquipmentStatusEvent>(RealtimeMethods.EquipmentStatusChanged));
        _published.Clear();

        var client = await _factory.ClientAsAsync("operator");
        var response = await client.PostAsync($"/api/alarms/{cleared.Id}/acknowledge", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var acknowledged = Assert.Single(_published.Events);
        Assert.Equal(RealtimeMethods.AlarmAcknowledged, acknowledged.Method);
        Assert.Equal("operator", Assert.IsType<AlarmDto>(acknowledged.Payload).AcknowledgedBy);
    }

    [Fact]
    public async Task Publisher_failure_does_not_fail_the_command()
    {
        await using var failing = api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.Replace(ServiceDescriptor.Singleton<IRealtimePublisher>(new ThrowingPublisher()))));
        var driver = new ProductionDriver(failing);
        var wo = await driver.CreateReleasedWorkOrderAsync();
        var raws = await driver.MaterialLotsAsync("NCM811");

        await driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);

        Assert.Equal(EquipmentStatus.Running, (await driver.EquipmentAsync("MX01")).Status);
    }

    [Fact]
    public async Task Committed_change_is_published_even_if_the_request_is_cancelled_after_commit()
    {
        using var request = new CancellationTokenSource();
        var options = new DbContextOptionsBuilder<MesDbContext>()
            .UseNpgsql(_factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Mes"))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new CancelAfterCommit(request))
            .Options;
        await using var db = new MesDbContext(
            options, TimeProvider.System, new ChangeFeed(_published, NullLogger<ChangeFeed>.Instance));

        var result = await db.ExecuteInTransactionAsync(async token =>
        {
            var equipment = await db.Set<EquipmentEntity>().SingleAsync(e => e.Code == "MX01", token);
            return equipment.StartMaintenance();
        }, request.Token);

        Assert.True(result.IsSuccess);
        Assert.True(request.IsCancellationRequested);
        Assert.Equal(
            [new EquipmentStatusEvent("MX01", EquipmentStatus.Maintenance)],
            _published.Payloads<EquipmentStatusEvent>(RealtimeMethods.EquipmentStatusChanged));
    }

    /// <summary>Simulates the caller aborting the request in the gap between commit and publish.</summary>
    private sealed class CancelAfterCommit(CancellationTokenSource request) : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            request.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IRealtimePublisher
    {
        public Task PublishAsync(IReadOnlyList<RealtimeEvent> events, CancellationToken ct) =>
            throw new InvalidOperationException("Hub is down");
    }
}
