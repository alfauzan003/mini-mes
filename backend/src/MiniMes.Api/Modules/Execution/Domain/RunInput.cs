namespace MiniMes.Api.Modules.Execution.Domain;

public class RunInput
{
    private RunInput()
    {
    }

    internal RunInput(Guid runId, Guid lotId, RunInputRole role)
    {
        RunId = runId;
        LotId = lotId;
        Role = role;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunId { get; private set; }
    public Guid LotId { get; private set; }
    public RunInputRole Role { get; private set; }

    /// <summary>Set when the run ends; null while the run is open.</summary>
    public decimal? ConsumedQty { get; private set; }

    internal void Consume(decimal qty) => ConsumedQty = qty;
}
