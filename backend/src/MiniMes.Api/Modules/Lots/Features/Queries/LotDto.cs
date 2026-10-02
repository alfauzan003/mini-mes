using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Lots.Features.Queries;

/// <param name="CurrentEquipment">Equipment code.</param>
/// <param name="CurrentCarrier">Carrier code.</param>
public sealed record LotDto(
    string LotId,
    LotType Type,
    Polarity Polarity,
    string? ProductCode,
    string? MaterialCode,
    string? WorkOrderNumber,
    decimal Qty,
    string Uom,
    LotStatus Status,
    QualityStatus Quality,
    OperationCode? CurrentOperation,
    OperationCode? NextOperation,
    string? CurrentEquipment,
    string? CurrentCarrier,
    DateTimeOffset CreatedAt);

/// <param name="Equipment">Equipment code.</param>
/// <param name="Carrier">Carrier code.</param>
/// <param name="User">Username of whoever caused the event.</param>
public sealed record LotEventDto(
    long Id,
    LotEventType Type,
    OperationCode? Operation,
    string? Equipment,
    string? Carrier,
    Guid? RunId,
    string User,
    decimal? Qty,
    string? Note,
    DateTimeOffset OccurredAt);
