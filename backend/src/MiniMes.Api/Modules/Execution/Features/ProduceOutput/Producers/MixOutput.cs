using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.ProduceOutput.Producers;

/// <summary>Mixing makes exactly one slurry lot, with no carrier, from every RAW input.</summary>
public static class MixOutput
{
    public static async Task<Result> ProduceAsync(
        ProduceContext ctx, IReadOnlyList<OutputLine> lines, CancellationToken ct)
    {
        if (ctx.Run.Outputs.Count > 0)
        {
            return ProduceContext.InvalidSet(OperationCode.Mix, "this run already produced its slurry lot");
        }

        if (lines.Count != 1)
        {
            return ProduceContext.InvalidSet(OperationCode.Mix, "expected exactly one slurry line");
        }

        var line = lines[0];
        if (ProduceContext.NormalizeCarrierCode(line.CarrierCode) is not null || line.Lane is not null)
        {
            return ProduceContext.InvalidSet(OperationCode.Mix, "slurry has no carrier and no lane");
        }

        if (line.GoodQty <= 0)
        {
            return ProduceContext.InvalidSet(OperationCode.Mix, "good quantity must be greater than zero");
        }

        var lotId = await ctx.Ids.NextLotIdAsync(LotType.Slurry, ctx.Product.Polarity, ctx.Equipment.Code, ct);
        if (!lotId.IsSuccess)
        {
            return lotId.Error!;
        }

        var slurry = ctx.AddOutputLot(
            lotId.Value, LotType.Slurry, "kg", OperationCode.Mix, line.GoodQty, ctx.Inputs);
        return ctx.Run.AddOutput(slurry.Id, null, null, line.GoodQty, line.RejectQty);
    }
}
