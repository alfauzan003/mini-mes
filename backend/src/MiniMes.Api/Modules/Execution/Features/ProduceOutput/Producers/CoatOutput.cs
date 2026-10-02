using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.ProduceOutput.Producers;

/// <summary>Coating makes one electrode roll per line, each on its own empty bobbin, from the foil and the slurry.</summary>
public static class CoatOutput
{
    public static async Task<Result> ProduceAsync(
        ProduceContext ctx, IReadOnlyList<OutputLine> lines, CancellationToken ct)
    {
        var invalid = Validate(lines);
        if (invalid is not null)
        {
            return invalid;
        }

        var carriers = new List<Carrier>();
        foreach (var line in lines)
        {
            var carrier = await ctx.FindCarrierAsync(line.CarrierCode, ct);
            if (!carrier.IsSuccess)
            {
                return carrier.Error!;
            }

            carriers.Add(carrier.Value);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var lotId = await ctx.Ids.NextLotIdAsync(
                LotType.Electrode, ctx.Product.Polarity, ctx.Equipment.Code, ct);
            if (!lotId.IsSuccess)
            {
                return lotId.Error!;
            }

            var roll = ctx.AddOutputLot(
                lotId.Value, LotType.Electrode, "m", OperationCode.Coat, lines[i].GoodQty, ctx.Inputs);
            var loaded = ctx.Load(carriers[i], roll, lines[i].GoodQty);
            if (!loaded.IsSuccess)
            {
                return loaded;
            }

            var added = ctx.Run.AddOutput(roll.Id, carriers[i].Id, null, lines[i].GoodQty, lines[i].RejectQty);
            if (!added.IsSuccess)
            {
                return added;
            }
        }

        return Result.Success();
    }

    private static Error? Validate(IReadOnlyList<OutputLine> lines)
    {
        if (lines.Count == 0)
        {
            return ProduceContext.InvalidSet(OperationCode.Coat, "expected at least one roll");
        }

        var codes = lines.Select(l => ProduceContext.NormalizeCarrierCode(l.CarrierCode)).ToList();
        if (codes.Any(c => c is null) || lines.Any(l => l.Lane is not null))
        {
            return ProduceContext.InvalidSet(OperationCode.Coat, "every roll needs a bobbin and has no lane");
        }

        if (lines.Any(l => l.GoodQty <= 0))
        {
            return ProduceContext.InvalidSet(OperationCode.Coat, "good length must be greater than zero");
        }

        return codes.Distinct().Count() != codes.Count
            ? ProduceContext.InvalidSet(OperationCode.Coat, "a bobbin can hold only one roll")
            : null;
    }
}
