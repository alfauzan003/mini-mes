using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Execution;

/// <param name="EquipmentCode">Equipment that ran (or is running) the step.</param>
/// <param name="Operator">Username of the operator who tracked in.</param>
/// <param name="ParentLotId">String lot ID of the primary input; null when the operation has none (MIX).</param>
public sealed record RunDto(
    Guid Id,
    string EquipmentCode,
    string WorkOrderNumber,
    Guid WorkOrderOperationId,
    OperationCode Operation,
    string Operator,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    decimal GoodQty,
    decimal RejectQty,
    string? ParentLotId,
    IReadOnlyList<RunInputDto> Inputs,
    IReadOnlyList<RunOutputDto> Outputs);

/// <param name="Qty">The lot's current quantity.</param>
/// <param name="ConsumedQty">Set when the run ends; null while it is open.</param>
public sealed record RunInputDto(
    string LotId, LotType Type, RunInputRole Role, decimal Qty, string Uom, decimal? ConsumedQty);

/// <param name="LotId">Null for a lane that produced no lot.</param>
/// <param name="CarrierCode">Carrier the output was placed on, when any.</param>
public sealed record RunOutputDto(
    string? LotId, string? CarrierCode, int? Lane, decimal GoodQty, decimal RejectQty);

public sealed record TrackInRequest(string EquipmentCode, Guid WorkOrderOperationId, IReadOnlyList<string> Inputs);

/// <param name="CarrierCode">Carrier the output goes onto; null when the operation uses none.</param>
/// <param name="Lane">Slitting lane, 1-based; null for operations without lanes.</param>
public sealed record OutputLine(string? CarrierCode, int? Lane, decimal GoodQty, decimal RejectQty);

public sealed record ProduceOutputRequest(IReadOnlyList<OutputLine> Outputs);
