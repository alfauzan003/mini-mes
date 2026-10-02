using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Quality.Features.Specs;

/// <param name="Lsl">Lower spec limit, inclusive.</param>
/// <param name="Usl">Upper spec limit, inclusive.</param>
public sealed record InspectionSpecDto(
    Guid Id, string ProductCode, OperationCode Operation, string ItemName, string Unit, decimal Lsl, decimal Usl, int Seq);
