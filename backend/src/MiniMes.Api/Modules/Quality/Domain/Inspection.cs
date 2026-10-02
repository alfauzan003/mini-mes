using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Quantities;
using MiniMes.Api.Shared.Results;
using DefectCodeEntity = MiniMes.Api.Modules.Quality.Domain.DefectCode;
using Outcome = MiniMes.Api.Shared.Results.Result;

namespace MiniMes.Api.Modules.Quality.Domain;

/// <summary>One quality inspection of a lot. Recording it does not touch the lot; the caller applies the outcome.</summary>
public class Inspection
{
    /// <summary>Largest magnitude a numeric(12,4) measurement column can hold.</summary>
    internal const decimal MaxMeasuredValue = 99_999_999.9999m;

    private readonly List<InspectionMeasurement> _measurements = [];

    private Inspection()
    {
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid LotId { get; private set; }
    public OperationCode Operation { get; private set; }
    public Guid InspectorId { get; private set; }
    public DateTimeOffset InspectedAt { get; private set; }
    public InspectionResult Result { get; private set; }
    public string? DefectCode { get; private set; }
    public string? Reason { get; private set; }
    public decimal? RejectQty { get; private set; }
    public Disposition? Disposition { get; private set; }
    public Guid? DispositionById { get; private set; }
    public DateTimeOffset? DispositionAt { get; private set; }
    public string? DispositionReason { get; private set; }

    public IReadOnlyList<InspectionMeasurement> Measurements => _measurements;

    public static Result<Inspection> Record(
        Lot lot,
        IReadOnlyList<InspectionSpec> specs,
        IReadOnlyList<(Guid SpecId, decimal Value)> values,
        DefectCodeEntity? defect,
        string? reason,
        decimal? rejectQty,
        Guid inspectorId,
        DateTimeOffset now)
    {
        if (specs.Count == 0)
        {
            return new Error(ErrorCodes.NoInspectionSpec, "No inspection spec is defined for this lot.");
        }

        if (lot.CurrentOperation is not { } operation)
        {
            return new Error(ErrorCodes.LotNotAvailable, $"Lot {lot.LotId} has not been produced by an operation.");
        }

        var measurementError = CheckValues(specs, values);
        if (measurementError is not null)
        {
            return measurementError;
        }

        var inspection = new Inspection
        {
            LotId = lot.Id,
            Operation = operation,
            InspectorId = inspectorId,
            InspectedAt = now
        };

        var valueBySpec = values.ToDictionary(v => v.SpecId, v => v.Value);
        foreach (var spec in specs)
        {
            inspection._measurements.Add(new InspectionMeasurement(inspection.Id, spec, valueBySpec[spec.Id]));
        }

        inspection.Result = inspection._measurements.All(m => m.Judgment == Judgment.Ok)
            ? InspectionResult.Pass
            : InspectionResult.Fail;

        if (inspection.Result == InspectionResult.Pass)
        {
            return inspection;
        }

        if (defect is null || string.IsNullOrWhiteSpace(reason))
        {
            return new Error(ErrorCodes.DefectRequired, "A failed inspection needs a defect code and a reason.");
        }

        if (!defect.AppliesTo(operation))
        {
            return new Error(
                ErrorCodes.DefectCodeNotFound, $"Defect code {defect.Code} does not apply to {operation}.");
        }

        var rejectError = CheckRejectQty(rejectQty, lot);
        if (rejectError is not null)
        {
            return rejectError;
        }

        inspection.DefectCode = defect.Code;
        inspection.Reason = reason.Trim();
        inspection.RejectQty = rejectQty;
        return inspection;
    }

    /// <summary>Records the decision on a failed inspection; it can be made once.</summary>
    public Result SetDisposition(Disposition decision, Guid userId, string reason, DateTimeOffset now)
    {
        if (Result != InspectionResult.Fail || Disposition is not null)
        {
            return new Error(
                ErrorCodes.LotNotAvailable, "Only a failed inspection without a disposition can be dispositioned.");
        }

        Disposition = decision;
        DispositionById = userId;
        DispositionAt = now;
        DispositionReason = reason;
        return Outcome.Success();
    }

    private static Error? CheckValues(
        IReadOnlyList<InspectionSpec> specs, IReadOnlyList<(Guid SpecId, decimal Value)> values)
    {
        var specIds = specs.Select(s => s.Id).ToHashSet();
        var valueIds = values.Select(v => v.SpecId).ToList();
        if (valueIds.Count != specIds.Count || !valueIds.ToHashSet().SetEquals(specIds))
        {
            return new Error(ErrorCodes.InvalidMeasurements, "Give exactly one value for every inspection spec.");
        }

        foreach (var (_, value) in values)
        {
            var error = QuantityRules.CheckFits(value, "A measured value");
            if (error is not null)
            {
                return error;
            }

            if (Math.Abs(value) > MaxMeasuredValue)
            {
                return new Error(ErrorCodes.InvalidQuantity, "A measured value is out of range.");
            }
        }

        return null;
    }

    private static Error? CheckRejectQty(decimal? rejectQty, Lot lot)
    {
        if (rejectQty is not { } qty)
        {
            return null;
        }

        if (qty < 0)
        {
            return new Error(ErrorCodes.InvalidQuantity, "Reject quantity cannot be negative.");
        }

        var fits = QuantityRules.CheckFits(qty, "Reject quantity");
        if (fits is not null)
        {
            return fits;
        }

        return qty > lot.Qty
            ? new Error(ErrorCodes.QtyExceedsLot, $"Lot {lot.LotId} has only {lot.Qty} {lot.Uom}.")
            : null;
    }
}
