using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.WorkOrders.Features.Queries;

/// <param name="Operations">Route steps in route order.</param>
public sealed record WorkOrderDto(
    Guid Id,
    string Number,
    string ProductCode,
    string ProductName,
    Polarity Polarity,
    int TargetQty,
    int GoodCount,
    WorkOrderStatus Status,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    IReadOnlyList<WorkOrderOperationDto> Operations);

/// <param name="EquipmentCode">Equipment assigned to this step.</param>
public sealed record WorkOrderOperationDto(Guid Id, OperationCode Operation, int Seq, string EquipmentCode);
