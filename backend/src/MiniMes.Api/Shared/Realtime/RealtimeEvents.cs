using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Shared.Realtime;

/// <summary>The hub method names clients subscribe to.</summary>
public static class RealtimeMethods
{
    public const string EquipmentStatusChanged = nameof(EquipmentStatusChanged);
    public const string LotChanged = nameof(LotChanged);
    public const string WorkOrderProgressed = nameof(WorkOrderProgressed);
    public const string AlarmRaised = nameof(AlarmRaised);
    public const string AlarmCleared = nameof(AlarmCleared);
    public const string AlarmAcknowledged = nameof(AlarmAcknowledged);
    public const string ParameterReading = nameof(ParameterReading);
}

public sealed record EquipmentStatusEvent(string Code, EquipmentStatus Status);

public sealed record LotChangedEvent(string LotId, LotStatus Status, QualityStatus Quality);

public sealed record WorkOrderProgressEvent(Guid Id, string Number, WorkOrderStatus Status, int GoodCount, int TargetQty);
