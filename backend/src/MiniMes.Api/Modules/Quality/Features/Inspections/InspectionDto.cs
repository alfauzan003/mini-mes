using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Quality.Features.Inspections;

/// <param name="Inspector">Username.</param>
/// <param name="DispositionBy">Username of whoever decided a failed inspection.</param>
/// <param name="Measurements">In spec sequence order, with the limits as they were when the inspection was recorded.</param>
public sealed record InspectionDto(
    Guid Id,
    string LotId,
    OperationCode Operation,
    string Inspector,
    DateTimeOffset InspectedAt,
    InspectionResult Result,
    string? DefectCode,
    string? DefectDescription,
    string? Reason,
    decimal? RejectQty,
    Disposition? Disposition,
    string? DispositionBy,
    DateTimeOffset? DispositionAt,
    string? DispositionReason,
    IReadOnlyList<MeasurementDto> Measurements);

public sealed record MeasurementDto(
    string ItemName, string Unit, decimal Lsl, decimal Usl, decimal Value, Judgment Judgment);
