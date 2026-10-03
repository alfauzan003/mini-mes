using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Quality;

namespace MiniMes.Api.Modules.Quality;

/// <summary>A lot needs inspection after an operation exactly when the product has specs for that operation.</summary>
public sealed class SpecInspectionRequirement(MesDbContext db) : IInspectionRequirement
{
    public Task<bool> IsRequiredAsync(Guid productId, OperationCode operation, CancellationToken ct) =>
        db.Set<InspectionSpec>().AnyAsync(s => s.ProductId == productId && s.Operation == operation, ct);
}
