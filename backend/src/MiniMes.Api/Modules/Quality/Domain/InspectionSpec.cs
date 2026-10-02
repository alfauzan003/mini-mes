using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Domain;

/// <summary>One measured item of an operation for a product, with its inclusive spec limits.</summary>
public class InspectionSpec
{
    private InspectionSpec()
    {
    }

    public InspectionSpec(
        Guid productId, OperationCode operation, string itemName, string unit, decimal lsl, decimal usl, int seq)
    {
        if (lsl >= usl)
        {
            throw new ArgumentException("Lower spec limit must be below the upper spec limit.", nameof(lsl));
        }

        ProductId = productId;
        Operation = operation;
        ItemName = itemName;
        Unit = unit;
        Lsl = lsl;
        Usl = usl;
        Seq = seq;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public OperationCode Operation { get; private set; }
    public string ItemName { get; private set; } = "";
    public string Unit { get; private set; } = "";
    public decimal Lsl { get; private set; }
    public decimal Usl { get; private set; }
    public int Seq { get; private set; }

    public Result UpdateLimits(decimal lsl, decimal usl)
    {
        if (lsl >= usl)
        {
            return new Error(ErrorCodes.InvalidSpecLimits, "Lower spec limit must be below the upper spec limit.");
        }

        Lsl = lsl;
        Usl = usl;
        return Result.Success();
    }

    /// <summary>OK when the value lies within the limits, both limits included.</summary>
    public Judgment Judge(decimal value) => value >= Lsl && value <= Usl ? Judgment.Ok : Judgment.Ng;
}
