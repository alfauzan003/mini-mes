using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.ProduceOutput.Producers;

/// <summary>
/// Slitting fills the whole lane grid at once. A lane with good output makes a pancake on its own empty core;
/// a lane without records only its reject. Without an inspection spec for slitting, pancakes finish at once.
/// </summary>
public static class SlitOutput
{
    public static async Task<Result> ProduceAsync(
        ProduceContext ctx, IReadOnlyList<OutputLine> lines, CancellationToken ct)
    {
        var invalid = Validate(ctx, lines);
        if (invalid is not null)
        {
            return invalid;
        }

        var ordered = lines.OrderBy(l => l.Lane).ToList();
        var carriers = new Dictionary<int, Carrier>();
        foreach (var line in ordered.Where(l => l.GoodQty > 0))
        {
            var carrier = await ctx.FindCarrierAsync(line.CarrierCode, ct);
            if (!carrier.IsSuccess)
            {
                return carrier.Error!;
            }

            carriers[line.Lane!.Value] = carrier.Value;
        }

        var parent = ctx.Inputs[0];
        var needsInspection = await ctx.Inspection.IsRequiredAsync(ctx.Product.Id, OperationCode.Slit, ct);
        foreach (var line in ordered)
        {
            var lane = line.Lane!.Value;
            if (line.GoodQty <= 0)
            {
                var skipped = ctx.Run.AddOutput(null, null, lane, 0m, line.RejectQty);
                if (!skipped.IsSuccess)
                {
                    return skipped;
                }

                continue;
            }

            var pancake = ctx.AddOutputLot(
                LotIdFormat.Pancake(parent.LotId, lane), LotType.Pancake, "m", OperationCode.Slit,
                line.GoodQty, [parent]);
            var loaded = ctx.Load(carriers[lane], pancake, line.GoodQty);
            if (!loaded.IsSuccess)
            {
                return loaded;
            }

            if (!needsInspection)
            {
                var finished = pancake.Finish();
                if (!finished.IsSuccess)
                {
                    return finished;
                }

                ctx.Record(pancake, LotEventType.Finish);
                ctx.WorkOrder.RegisterFinishedPancake();
            }

            var added = ctx.Run.AddOutput(pancake.Id, carriers[lane].Id, lane, line.GoodQty, line.RejectQty);
            if (!added.IsSuccess)
            {
                return added;
            }
        }

        return Result.Success();
    }

    private static Error? Validate(ProduceContext ctx, IReadOnlyList<OutputLine> lines)
    {
        if (ctx.Run.Outputs.Count > 0)
        {
            return ProduceContext.InvalidSet(OperationCode.Slit, "this run already produced its lanes");
        }

        var laneCount = ctx.Equipment.LaneCount ?? 0;
        if (laneCount <= 0)
        {
            return ProduceContext.InvalidSet(OperationCode.Slit, $"equipment {ctx.Equipment.Code} has no lanes");
        }

        if (lines.Count != laneCount)
        {
            return ProduceContext.InvalidSet(OperationCode.Slit, $"expected one line for each of the {laneCount} lanes");
        }

        var lanes = lines.Select(l => l.Lane).Order().ToList();
        if (!lanes.SequenceEqual(Enumerable.Range(1, laneCount).Select(l => (int?)l)))
        {
            return ProduceContext.InvalidSet(OperationCode.Slit, $"lanes must be exactly 1 to {laneCount}, each once");
        }

        var codes = lines.Where(l => l.GoodQty > 0).Select(l => ProduceContext.NormalizeCarrierCode(l.CarrierCode)).ToList();
        if (codes.Any(c => c is null))
        {
            return ProduceContext.InvalidSet(OperationCode.Slit, "every lane with good output needs a pancake core");
        }

        return codes.Distinct().Count() != codes.Count
            ? ProduceContext.InvalidSet(OperationCode.Slit, "a pancake core can hold only one pancake")
            : null;
    }
}
