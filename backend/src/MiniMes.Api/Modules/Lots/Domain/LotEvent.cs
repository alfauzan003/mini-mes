using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Lots.Domain;

/// <summary>One immutable entry in the history of a lot. The table is append-only (enforced by a database trigger).</summary>
public class LotEvent
{
    private LotEvent()
    {
    }

    public long Id { get; private set; }
    public Guid LotId { get; private set; }
    public LotEventType Type { get; private set; }
    public OperationCode? Operation { get; private set; }
    public Guid? EquipmentId { get; private set; }
    public Guid? CarrierId { get; private set; }
    public Guid? RunId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal? Qty { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Captures the current operation, equipment and carrier of the lot alongside the event.</summary>
    public static LotEvent Record(
        Lot lot, LotEventType type, Guid userId, DateTimeOffset at,
        Guid? runId = null, decimal? qty = null, string? note = null) => new()
    {
        LotId = lot.Id,
        Type = type,
        Operation = lot.CurrentOperation,
        EquipmentId = lot.CurrentEquipmentId,
        CarrierId = lot.CurrentCarrierId,
        RunId = runId,
        UserId = userId,
        Qty = qty,
        Note = note,
        OccurredAt = at
    };
}
