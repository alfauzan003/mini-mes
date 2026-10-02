namespace MiniMes.Api.Modules.Execution.Domain;

public class RunOutput
{
    private RunOutput()
    {
    }

    internal RunOutput(Guid runId, Guid? lotId, Guid? carrierId, int? lane, decimal goodQty, decimal rejectQty)
    {
        RunId = runId;
        LotId = lotId;
        CarrierId = carrierId;
        Lane = lane;
        GoodQty = goodQty;
        RejectQty = rejectQty;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunId { get; private set; }
    public Guid? LotId { get; private set; }
    public Guid? CarrierId { get; private set; }
    public int? Lane { get; private set; }
    public decimal GoodQty { get; private set; }
    public decimal RejectQty { get; private set; }
}
