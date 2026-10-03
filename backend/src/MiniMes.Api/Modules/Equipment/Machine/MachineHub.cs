using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Alarms;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Parameters;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Realtime;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Machine;

/// <summary>
/// The channel equipment simulators use: they read their configuration, report readings and raise or clear alarms,
/// and receive equipment status changes and injected faults. Failures reach the caller as a
/// <see cref="HubException"/> whose message starts with the error code.
/// </summary>
[Authorize(Policy = Policies.Machine)]
public sealed class MachineHub(
    MesDbContext db,
    ReadingRecorder recorder,
    AlarmService alarms,
    IRealtimePublisher publisher,
    SimulatorPresence presence) : Hub
{
    public const string SimulatorsGroup = "simulators";

    private const string CountedKey = "simulator-counted";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SimulatorsGroup);
        Context.Items[CountedKey] = true;
        presence.Increment();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.Remove(CountedKey))
        {
            presence.Decrement();
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<IReadOnlyList<MachineEquipmentState>> GetEquipmentStates()
    {
        var ct = Context.ConnectionAborted;
        var equipment = await db.Set<EquipmentEntity>().AsNoTracking().OrderBy(e => e.Code).ToListAsync(ct);
        var parameters = await db.Set<ParameterDefinition>().AsNoTracking().OrderBy(p => p.Seq).ToListAsync(ct);
        var codes = await db.Set<AlarmCode>().AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        var active = await db.Set<Alarm>().AsNoTracking()
            .Where(a => a.ClearedAt == null)
            .Select(a => new { a.EquipmentId, a.AlarmCode })
            .ToListAsync(ct);

        return [.. equipment.Select(e => new MachineEquipmentState(
            e.Code,
            e.Operation,
            e.Status,
            [.. parameters.Where(p => p.Operation == e.Operation).Select(p => new MachineParameterDto(
                p.Name, p.Kind, p.Unit, p.Setpoint, p.Low, p.High, p.LowAlarmCode, p.HighAlarmCode))],
            [.. codes.Where(c => c.Operation == e.Operation).Select(c => new MachineAlarmCodeDto(c.Code, c.Severity))],
            [.. active.Where(a => a.EquipmentId == e.Id).Select(a => a.AlarmCode).Order(StringComparer.Ordinal)]))];
    }

    public async Task ReportReadings(string? equipmentCode, IReadOnlyList<ReadingInput?>? readings)
    {
        Require(equipmentCode, nameof(equipmentCode));
        if (readings is null || readings.Count == 0
            || readings.Any(r => r is null || string.IsNullOrWhiteSpace(r.Parameter)))
        {
            throw Invalid("readings must be a non-empty list of entries that each name a parameter.");
        }

        var result = await recorder.RecordAsync(
            equipmentCode!, [.. readings.Select(r => r!)], Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            throw Fail(result.Error!);
        }

        // The readings are already recorded, so the publish does not follow the connection's lifetime.
        await publisher.PublishAsync(
            [.. result.Value.Select(r => new RealtimeEvent(RealtimeAudience.Shopfloor, RealtimeMethods.ParameterReading, r))],
            CancellationToken.None);
    }

    public async Task RaiseAlarm(string? equipmentCode, string? alarmCode)
    {
        Require(equipmentCode, nameof(equipmentCode));
        Require(alarmCode, nameof(alarmCode));
        var result = await alarms.RaiseAsync(
            equipmentCode!.Trim().ToUpperInvariant(), alarmCode!.Trim(), Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            throw Fail(result.Error!);
        }
    }

    public async Task ClearAlarm(string? equipmentCode, string? alarmCode)
    {
        Require(equipmentCode, nameof(equipmentCode));
        Require(alarmCode, nameof(alarmCode));
        var result = await alarms.ClearAsync(
            equipmentCode!.Trim().ToUpperInvariant(), alarmCode!.Trim(), Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            throw Fail(result.Error!);
        }
    }

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid($"{name} is required.");
        }
    }

    private static HubException Invalid(string message) => new($"{ErrorCodes.InvalidInput}: {message}");

    private static HubException Fail(Error error) => new($"{error.Code}: {error.Message}");
}
