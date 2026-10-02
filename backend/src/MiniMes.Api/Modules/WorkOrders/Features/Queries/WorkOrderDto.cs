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
/// <param name="RunCount">Runs of this step, open or ended.</param>
/// <param name="OutputQty">Sum of the good quantity of this step's ended runs.</param>
public sealed record WorkOrderOperationDto(
    Guid Id, OperationCode Operation, int Seq, string EquipmentCode, int RunCount, decimal OutputQty);
