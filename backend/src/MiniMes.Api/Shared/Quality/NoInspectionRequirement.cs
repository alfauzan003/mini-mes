using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Shared.Quality;

/// <summary>Default until the quality module supplies real inspection rules: nothing needs inspecting.</summary>
public sealed class NoInspectionRequirement : IInspectionRequirement
{
    public Task<bool> IsRequiredAsync(Guid productId, OperationCode operation, CancellationToken ct) =>
        Task.FromResult(false);
}
