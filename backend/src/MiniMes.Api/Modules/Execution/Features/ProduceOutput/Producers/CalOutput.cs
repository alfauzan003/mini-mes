using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.ProduceOutput.Producers;

/// <summary>Calendering keeps the same lot: it moves to a new empty bobbin and its quantity becomes the good length.</summary>
public static class CalOutput
{
    public static async Task<Result> ProduceAsync(
        ProduceContext ctx, IReadOnlyList<OutputLine> lines, CancellationToken ct)
    {
        if (ctx.Run.Outputs.Count > 0)
        {
            return ProduceContext.InvalidSet(OperationCode.Cal, "this run already produced its output");
        }

        if (lines.Count != 1)
        {
            return ProduceContext.InvalidSet(OperationCode.Cal, "expected exactly one output line");
        }

        var line = lines[0];
        if (ProduceContext.NormalizeCarrierCode(line.CarrierCode) is null)
        {
            return ProduceContext.InvalidSet(OperationCode.Cal, "the roll needs the bobbin it moves to");
        }

        if (line.GoodQty <= 0)
        {
            return ProduceContext.InvalidSet(OperationCode.Cal, "good length must be greater than zero");
        }

        var carrier = await ctx.FindCarrierAsync(line.CarrierCode, ct);
        if (!carrier.IsSuccess)
        {
            return carrier.Error!;
        }

        var roll = ctx.Inputs[0];
        var previous = roll.CurrentCarrierId is { } previousId
            ? await ctx.Db.Set<Carrier>().SingleOrDefaultAsync(c => c.Id == previousId, ct)
            : null;

        var calendered = roll.CompleteCalendering(line.GoodQty, ctx.Product);
        if (!calendered.IsSuccess)
        {
            return calendered;
        }

        if (previous is not null)
        {
            ctx.Record(roll, LotEventType.CarrierUnload);
        }

        var loaded = ctx.Load(carrier.Value, roll, line.GoodQty);
        if (!loaded.IsSuccess)
        {
            return loaded;
        }

        previous?.Unload();
        return ctx.Run.AddOutput(roll.Id, carrier.Value.Id, null, line.GoodQty, line.RejectQty);
    }
}
