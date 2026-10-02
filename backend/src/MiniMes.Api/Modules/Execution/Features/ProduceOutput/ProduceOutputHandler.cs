using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Execution.Features.ProduceOutput.Producers;
using MiniMes.Api.Modules.Execution.Features.Queries;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Quality;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Execution.Features.ProduceOutput;

public sealed class ProduceOutputHandler(
    MesDbContext db,
    LotIdGenerator ids,
    IInspectionRequirement inspection,
    RunQueries queries,
    ICurrentUser user,
    TimeProvider time)
{
    public async Task<Result<RunDto>> HandleAsync(Guid runId, ProduceOutputRequest request, CancellationToken ct)
    {
        var lines = request.Outputs ?? [];
        var produced = await db.ExecuteInTransactionAsync<Guid>(token => ProduceAsync(runId, lines, token), ct);
        if (!produced.IsSuccess)
        {
            return produced.Error!;
        }

        return (await queries.GetAsync(runId, ct))!;
    }

    private async Task<Result<Guid>> ProduceAsync(Guid runId, IReadOnlyList<OutputLine> lines, CancellationToken ct)
    {
        // Lock first, load after: the run and its outputs must be read once any concurrent produce has committed.
        if (!await db.AcquireAsync(runId, ct))
        {
            return new Error(ErrorCodes.RunNotFound, $"Run '{runId}' was not found.", ErrorKind.NotFound);
        }

        var run = await db.Set<ProductionRun>()
            .Include(r => r.Inputs)
            .Include(r => r.Outputs)
            .SingleAsync(r => r.Id == runId, ct);

        if (!run.IsOpen)
        {
            return new Error(ErrorCodes.RunNotOpen, "The production run is already closed.");
        }

        if (lines.Any(l => l.GoodQty < 0 || l.RejectQty < 0 || l.GoodQty + l.RejectQty <= 0))
        {
            return new Error(
                ErrorCodes.InvalidQuantity,
                "Good and reject quantities cannot be negative, and each line needs a quantity above zero.");
        }

        var step = await db.Set<WorkOrderOperation>().SingleAsync(o => o.Id == run.WorkOrderOperationId, ct);
        var workOrder = await db.Set<WorkOrder>().SingleAsync(w => w.Id == step.WorkOrderId, ct);
        var product = await db.Set<Product>().Include(p => p.Route).SingleAsync(p => p.Id == workOrder.ProductId, ct);
        var equipment = await db.Set<EquipmentEntity>().SingleAsync(e => e.Id == run.EquipmentId, ct);
        var inputIds = run.Inputs.Select(i => i.LotId).ToArray();
        var inputs = await db.Set<Lot>().Where(l => inputIds.Contains(l.Id)).OrderBy(l => l.LotId).ToListAsync(ct);

        var context = new ProduceContext(
            db, ids, inspection, run, workOrder, product, equipment, inputs, user.UserId, time.GetUtcNow());
        var result = step.Operation switch
        {
            OperationCode.Mix => await MixOutput.ProduceAsync(context, lines, ct),
            OperationCode.Coat => await CoatOutput.ProduceAsync(context, lines, ct),
            OperationCode.Cal => await CalOutput.ProduceAsync(context, lines, ct),
            OperationCode.Slit => await SlitOutput.ProduceAsync(context, lines, ct),
            _ => ProduceContext.InvalidSet(step.Operation, "this operation does not produce output")
        };

        return result.IsSuccess ? run.Id : result.Error!;
    }
}
