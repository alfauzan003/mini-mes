using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Domain;

/// <summary>One equipment run of a work-order operation, from track-in to track-out.</summary>
public class ProductionRun
{
    private readonly List<RunInput> _inputs = [];
    private readonly List<RunOutput> _outputs = [];

    private ProductionRun()
    {
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkOrderOperationId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public Guid OperatorId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public decimal GoodQty { get; private set; }
    public decimal RejectQty { get; private set; }

    public IReadOnlyList<RunInput> Inputs => _inputs;

    public IReadOnlyList<RunOutput> Outputs => _outputs;

    public bool IsOpen => EndedAt is null;

    public Guid? PrimaryLotId => _inputs.FirstOrDefault(i => i.Role == RunInputRole.Primary)?.LotId;

    public static ProductionRun Start(
        Guid workOrderOperationId, Guid equipmentId, Guid operatorId,
        IEnumerable<(Guid LotId, RunInputRole Role)> inputs, DateTimeOffset now)
    {
        var run = new ProductionRun
        {
            WorkOrderOperationId = workOrderOperationId,
            EquipmentId = equipmentId,
            OperatorId = operatorId,
            StartedAt = now
        };
        foreach (var (lotId, role) in inputs)
        {
            run._inputs.Add(new RunInput(run.Id, lotId, role));
        }

        return run;
    }

    public Result AddOutput(Guid? lotId, Guid? carrierId, int? lane, decimal goodQty, decimal rejectQty)
    {
        if (!IsOpen)
        {
            return NotOpen();
        }

        if (goodQty < 0 || rejectQty < 0)
        {
            return new Error(ErrorCodes.InvalidQuantity, "Good and reject quantities cannot be negative.");
        }

        _outputs.Add(new RunOutput(Id, lotId, carrierId, lane, goodQty, rejectQty));
        return Result.Success();
    }

    /// <summary>Closes the run: records consumption per input lot and totals the outputs.</summary>
    public Result End(IReadOnlyDictionary<Guid, decimal> consumedByLot, DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return NotOpen();
        }

        foreach (var input in _inputs)
        {
            if (consumedByLot.TryGetValue(input.LotId, out var consumed))
            {
                input.Consume(consumed);
            }
        }

        GoodQty = _outputs.Sum(o => o.GoodQty);
        RejectQty = _outputs.Sum(o => o.RejectQty);
        EndedAt = now;
        return Result.Success();
    }

    private static Error NotOpen() => new(ErrorCodes.RunNotOpen, "The production run is already closed.");
}
