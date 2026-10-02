using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Lots.Domain;

public class Lot
{
    private Lot()
    {
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string LotId { get; private set; } = "";
    public LotType Type { get; private set; }
    public Polarity Polarity { get; private set; }
    public Guid? MaterialId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public decimal Qty { get; private set; }
    public string Uom { get; private set; } = "";
    public LotStatus Status { get; private set; }
    public QualityStatus Quality { get; private set; }
    public OperationCode? CurrentOperation { get; private set; }
    public OperationCode? NextOperation { get; private set; }
    public Guid? CurrentEquipmentId { get; private set; }
    public Guid? CurrentCarrierId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public uint Version { get; private set; }

    /// <summary>True when no further operation follows on the route.</summary>
    public bool IsFinal => NextOperation is null;

    public bool CanBeInspected => Status == LotStatus.Wait && Quality == QualityStatus.None;

    /// <summary>Receives incoming material: waiting, already passed incoming quality, headed for MIX (RAW) or COAT (FOIL).</summary>
    public static Lot RegisterMaterial(string lotId, Material material, decimal qty, DateTimeOffset now) => new()
    {
        LotId = lotId,
        Type = material.Kind,
        Polarity = material.Polarity,
        MaterialId = material.Id,
        Qty = qty,
        Uom = material.Uom,
        Status = LotStatus.Wait,
        Quality = QualityStatus.Pass,
        NextOperation = material.Kind == LotType.Raw ? OperationCode.Mix : OperationCode.Coat,
        CreatedAt = now
    };

    /// <summary>A lot produced by <paramref name="producedBy"/>, waiting for the next step of the product route.</summary>
    public static Lot CreateOutput(
        string lotId, LotType type, Product product, Guid workOrderId, decimal qty, string uom,
        OperationCode producedBy, Guid equipmentId, DateTimeOffset now) => new()
    {
        LotId = lotId,
        Type = type,
        Polarity = product.Polarity,
        ProductId = product.Id,
        WorkOrderId = workOrderId,
        Qty = qty,
        Uom = uom,
        Status = LotStatus.Wait,
        Quality = QualityStatus.None,
        CurrentOperation = producedBy,
        NextOperation = product.NextAfter(producedBy),
        CurrentEquipmentId = equipmentId,
        CreatedAt = now
    };

    public Result TrackIn(Guid equipmentId)
    {
        if (Status != LotStatus.Wait)
        {
            return NotAvailable();
        }

        Status = LotStatus.Run;
        CurrentEquipmentId = equipmentId;
        return Result.Success();
    }

    /// <summary>Takes <paramref name="qty"/> out of a running lot: fully used up becomes CONSUMED, otherwise it returns to WAIT.</summary>
    public Result Consume(decimal qty)
    {
        if (Status != LotStatus.Run)
        {
            return NotAvailable();
        }

        if (qty < 0)
        {
            return new Error(ErrorCodes.InvalidQuantity, "Quantity cannot be negative.");
        }

        if (qty > Qty)
        {
            return new Error(ErrorCodes.QtyExceedsLot, $"Lot {LotId} has only {Qty} {Uom}.");
        }

        Qty -= qty;
        if (Qty == 0)
        {
            Status = LotStatus.Consumed;
            CurrentCarrierId = null;
        }
        else
        {
            Status = LotStatus.Wait;
        }

        return Result.Success();
    }

    /// <summary>Gives a running lot back untouched: quantity, carrier and next operation stay as they were.</summary>
    public Result ReturnToWait()
    {
        if (Status != LotStatus.Run)
        {
            return NotAvailable();
        }

        Status = LotStatus.Wait;
        return Result.Success();
    }

    /// <summary>Calendering keeps the same lot: it returns to WAIT with the good length, quality reset, headed for the next step.</summary>
    public Result CompleteCalendering(decimal goodQty, Product product)
    {
        if (Status != LotStatus.Run)
        {
            return NotAvailable();
        }

        Status = LotStatus.Wait;
        Qty = goodQty;
        Quality = QualityStatus.None;
        CurrentOperation = OperationCode.Cal;
        NextOperation = product.NextAfter(OperationCode.Cal);
        return Result.Success();
    }

    public void PlaceOnCarrier(Guid? carrierId) => CurrentCarrierId = carrierId;

    public Result Finish()
    {
        if (Type != LotType.Pancake || Status != LotStatus.Wait)
        {
            return NotAvailable();
        }

        Status = LotStatus.Finished;
        return Result.Success();
    }

    /// <summary>Pass keeps the lot waiting with quality PASS; fail holds it with quality FAIL.</summary>
    public Result ApplyInspection(InspectionResult result)
    {
        if (!CanBeInspected)
        {
            return NotAvailable();
        }

        if (result == InspectionResult.Pass)
        {
            Quality = QualityStatus.Pass;
        }
        else
        {
            Quality = QualityStatus.Fail;
            Status = LotStatus.Hold;
        }

        return Result.Success();
    }

    public Result Hold()
    {
        if (Status != LotStatus.Wait)
        {
            return NotAvailable();
        }

        Status = LotStatus.Hold;
        return Result.Success();
    }

    /// <summary>Releasing a lot held for failing inspection accepts it: its quality becomes PASS.</summary>
    public Result Release()
    {
        if (Status != LotStatus.Hold)
        {
            return NotAvailable();
        }

        Status = LotStatus.Wait;
        if (Quality == QualityStatus.Fail)
        {
            Quality = QualityStatus.Pass;
        }

        return Result.Success();
    }

    public Result Scrap()
    {
        if (Status != LotStatus.Hold)
        {
            return NotAvailable();
        }

        Status = LotStatus.Scrapped;
        CurrentCarrierId = null;
        return Result.Success();
    }

    private Error NotAvailable() =>
        new(ErrorCodes.LotNotAvailable, $"Lot {LotId} is {Status} and cannot take this action.");
}
