using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Alarms.Features.Queries;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Shared.Realtime;

/// <summary>What a transaction is about to save that clients care about, taken before the change tracker forgets it.</summary>
public sealed class PendingChanges
{
    internal List<EquipmentStatusEvent> Equipment { get; } = [];
    internal List<LotChangedEvent> Lots { get; } = [];
    internal List<WorkOrderProgressEvent> WorkOrders { get; } = [];
    internal List<Guid> AlarmsRaised { get; } = [];
    internal List<Guid> AlarmsCleared { get; } = [];
    internal List<Guid> AlarmsAcknowledged { get; } = [];

    public bool IsEmpty =>
        Equipment.Count == 0 && Lots.Count == 0 && WorkOrders.Count == 0
        && AlarmsRaised.Count == 0 && AlarmsCleared.Count == 0 && AlarmsAcknowledged.Count == 0;
}

/// <summary>
/// Turns a committed transaction's changes into shop floor events. <see cref="MesDbContext"/> captures before saving
/// and publishes after commit, so a rolled-back command publishes nothing. Takes no context of its own, which keeps
/// the context free to depend on it.
/// </summary>
public sealed class ChangeFeed(IRealtimePublisher publisher, ILogger<ChangeFeed> logger)
{
    public PendingChanges Capture(ChangeTracker tracker)
    {
        var changes = new PendingChanges();
        foreach (var entry in tracker.Entries())
        {
            switch (entry.Entity)
            {
                case EquipmentEntity equipment
                    when entry.State == EntityState.Modified && entry.Property(nameof(EquipmentEntity.Status)).IsModified:
                    changes.Equipment.Add(new EquipmentStatusEvent(equipment.Code, equipment.Status));
                    break;
                case Lot lot when entry.State is EntityState.Added or EntityState.Modified:
                    changes.Lots.Add(new LotChangedEvent(lot.LotId, lot.Status, lot.Quality));
                    break;
                case WorkOrder wo when entry.State is EntityState.Added or EntityState.Modified:
                    changes.WorkOrders.Add(
                        new WorkOrderProgressEvent(wo.Id, wo.Number, wo.Status, wo.GoodCount, wo.TargetQty));
                    break;
                case Alarm alarm when entry.State == EntityState.Added:
                    changes.AlarmsRaised.Add(alarm.Id);
                    break;
                case Alarm alarm when entry.State == EntityState.Modified:
                    if (entry.Property(nameof(Alarm.ClearedAt)).IsModified)
                    {
                        changes.AlarmsCleared.Add(alarm.Id);
                    }

                    if (entry.Property(nameof(Alarm.AcknowledgedAt)).IsModified)
                    {
                        changes.AlarmsAcknowledged.Add(alarm.Id);
                    }

                    break;
            }
        }

        return changes;
    }

    /// <summary>
    /// Publishes the captured changes; call only after commit, with a token that is not the request's, since the
    /// work is already saved. Never throws.
    /// </summary>
    public async Task PublishAsync(PendingChanges changes, MesDbContext db, CancellationToken ct)
    {
        if (changes.IsEmpty)
        {
            return;
        }

        try
        {
            var events = new List<RealtimeEvent>();
            events.AddRange(changes.Equipment.Select(e => Shopfloor(RealtimeMethods.EquipmentStatusChanged, e)));
            events.AddRange(changes.Equipment.Select(e => new RealtimeEvent(
                RealtimeAudience.Simulators, RealtimeMethods.EquipmentStateChanged, e)));
            events.AddRange(changes.Lots.Select(l => Shopfloor(RealtimeMethods.LotChanged, l)));
            events.AddRange(changes.WorkOrders.Select(w => Shopfloor(RealtimeMethods.WorkOrderProgressed, w)));

            // Sent before the alarm lookup so a slow query cannot hold back the equipment state the simulators wait on.
            if (events.Count > 0)
            {
                await publisher.PublishAsync(events, ct);
                events = [];
            }

            Guid[] alarmIds = [.. changes.AlarmsRaised, .. changes.AlarmsCleared, .. changes.AlarmsAcknowledged];
            if (alarmIds.Length > 0)
            {
                var alarms = (await AlarmQueries.LoadManyAsync(db, [.. alarmIds.Distinct()], ct)).ToDictionary(a => a.Id);
                AddAlarms(events, RealtimeMethods.AlarmRaised, changes.AlarmsRaised, alarms);
                AddAlarms(events, RealtimeMethods.AlarmCleared, changes.AlarmsCleared, alarms);
                AddAlarms(events, RealtimeMethods.AlarmAcknowledged, changes.AlarmsAcknowledged, alarms);
            }

            if (events.Count > 0)
            {
                await publisher.PublishAsync(events, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not publish committed changes");
        }
    }

    private static void AddAlarms(
        List<RealtimeEvent> events, string method, List<Guid> ids, Dictionary<Guid, AlarmDto> alarms)
    {
        foreach (var id in ids)
        {
            if (alarms.TryGetValue(id, out var alarm))
            {
                events.Add(Shopfloor(method, alarm));
            }
        }
    }

    private static RealtimeEvent Shopfloor(string method, object payload) =>
        new(RealtimeAudience.Shopfloor, method, payload);
}
