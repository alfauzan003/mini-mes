using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Shared.Quality;

/// <summary>Tells execution whether lots produced by an operation must pass inspection before the next step.</summary>
public interface IInspectionRequirement
{
    Task<bool> IsRequiredAsync(Guid productId, OperationCode operation, CancellationToken ct);
}
