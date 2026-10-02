using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Quantities;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Lots.Features.RegisterMaterial;

public sealed record RegisterMaterialRequest(string MaterialCode, decimal Qty);

public sealed class RegisterMaterialHandler(
    MesDbContext db, LotIdGenerator lotIds, LotQueries queries, ICurrentUser user, TimeProvider time)
{
    public async Task<Result<LotDto>> HandleAsync(RegisterMaterialRequest request, CancellationToken ct)
    {
        if (request.Qty <= 0)
        {
            return new Error(ErrorCodes.InvalidQuantity, "Quantity must be greater than 0.");
        }

        if (QuantityRules.CheckFits(request.Qty, "Quantity") is { } unfit)
        {
            return unfit;
        }

        var code = (request.MaterialCode ?? "").Trim().ToUpperInvariant();
        var registered = await db.ExecuteInTransactionAsync<string>(async token =>
        {
            var material = await db.Set<Material>().SingleOrDefaultAsync(m => m.Code == code, token);
            if (material is null)
            {
                return new Error(
                    ErrorCodes.MaterialNotFound, $"Material '{request.MaterialCode}' was not found.", ErrorKind.NotFound);
            }

            var lotId = await lotIds.NextLotIdAsync(material.Kind, material.Polarity, null, token);
            if (!lotId.IsSuccess)
            {
                return lotId;
            }

            var now = time.GetUtcNow();
            var lot = Lot.RegisterMaterial(lotId.Value, material, request.Qty, now);
            db.Set<Lot>().Add(lot);
            db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Register, user.UserId, now, qty: request.Qty));
            return lotId;
        }, ct);

        if (!registered.IsSuccess)
        {
            return registered.Error!;
        }

        return (await queries.GetAsync(registered.Value, ct))!;
    }
}

public static class RegisterMaterialEndpoint
{
    public static void MapRegisterMaterial(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/lots/materials", async (
            RegisterMaterialRequest request, RegisterMaterialHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(request, ct))
                .ToHttpResult(lot => TypedResults.Created($"/api/lots/{lot.LotId}", lot)))
            .RequireAuthorization(Policies.Plan);
}
