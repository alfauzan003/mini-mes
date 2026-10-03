using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Quality;

public class InspectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Inspector = Guid.NewGuid();

    private readonly Lot _lot = TestData.CoatedRoll();
    private readonly InspectionSpec _loading = TestData.CathodeSpec(OperationCode.Coat, "Loading weight", "mg/cm²", 19.5m, 20.5m, 1);
    private readonly InspectionSpec _thickness = TestData.CathodeSpec(OperationCode.Coat, "Thickness", "µm", 100m, 110m, 2);

    private IReadOnlyList<InspectionSpec> Specs => [_loading, _thickness];

    private Result<Inspection> Record(
        IReadOnlyList<(Guid SpecId, decimal Value)> values,
        DefectCode? defect = null,
        string? reason = null,
        decimal? rejectQty = null,
        IReadOnlyList<InspectionSpec>? specs = null) =>
        Inspection.Record(_lot, specs ?? Specs, values, defect, reason, rejectQty, Inspector, Now);

    private IReadOnlyList<(Guid SpecId, decimal Value)> InLimits() => [(_loading.Id, 20m), (_thickness.Id, 105m)];

    private IReadOnlyList<(Guid SpecId, decimal Value)> OutOfLimits() => [(_loading.Id, 21m), (_thickness.Id, 105m)];

    private static DefectCode CoatDefect() => new("CT-LOAD", "Loading out of range", OperationCode.Coat);

    [Fact]
    public void All_values_in_limits_is_pass_and_ignores_defect_fields()
    {
        var result = Record(InLimits(), CoatDefect(), "ignored", 5m);

        Assert.True(result.IsSuccess);
        var inspection = result.Value;
        Assert.Equal(InspectionResult.Pass, inspection.Result);
        Assert.Null(inspection.DefectCode);
        Assert.Null(inspection.Reason);
        Assert.Null(inspection.RejectQty);
        Assert.Equal(_lot.Id, inspection.LotId);
        Assert.Equal(OperationCode.Coat, inspection.Operation);
        Assert.Equal(Inspector, inspection.InspectorId);
        Assert.Equal(Now, inspection.InspectedAt);
        Assert.All(inspection.Measurements, m => Assert.Equal(Judgment.Ok, m.Judgment));
    }

    [Fact]
    public void One_value_out_of_limits_is_fail()
    {
        var result = Record(OutOfLimits(), CoatDefect(), "Too heavy", 10m);

        Assert.True(result.IsSuccess);
        var inspection = result.Value;
        Assert.Equal(InspectionResult.Fail, inspection.Result);
        Assert.Equal("CT-LOAD", inspection.DefectCode);
        Assert.Equal("Too heavy", inspection.Reason);
        Assert.Equal(10m, inspection.RejectQty);
        Assert.Equal(Judgment.Ng, inspection.Measurements.Single(m => m.SpecId == _loading.Id).Judgment);
        Assert.Equal(Judgment.Ok, inspection.Measurements.Single(m => m.SpecId == _thickness.Id).Judgment);
    }

    [Fact]
    public void Fail_without_defect_or_reason_is_defect_required()
    {
        Assert.Equal(ErrorCodes.DefectRequired, Record(OutOfLimits(), null, "Too heavy").Error!.Code);
        Assert.Equal(ErrorCodes.DefectRequired, Record(OutOfLimits(), CoatDefect(), null).Error!.Code);
        Assert.Equal(ErrorCodes.DefectRequired, Record(OutOfLimits(), CoatDefect(), "   ").Error!.Code);
    }

    [Fact]
    public void Defect_for_another_operation_is_defect_code_not_found()
    {
        var burr = new DefectCode("SL-BURR", "Slit burr", OperationCode.Slit);

        Assert.Equal(ErrorCodes.DefectCodeNotFound, Record(OutOfLimits(), burr, "Burr").Error!.Code);
    }

    [Fact]
    public void General_defect_applies_to_any_operation()
    {
        var other = new DefectCode("GEN-OTHER", "Other", null);

        var result = Record(OutOfLimits(), other, "Something else");

        Assert.True(result.IsSuccess);
        Assert.Equal("GEN-OTHER", result.Value.DefectCode);
    }

    [Fact]
    public void Missing_measurement_is_invalid_measurements() =>
        Assert.Equal(ErrorCodes.InvalidMeasurements, Record([(_loading.Id, 20m)]).Error!.Code);

    [Fact]
    public void Duplicate_spec_is_invalid_measurements() =>
        Assert.Equal(
            ErrorCodes.InvalidMeasurements,
            Record([(_loading.Id, 20m), (_loading.Id, 20m), (_thickness.Id, 105m)]).Error!.Code);

    [Fact]
    public void Unknown_spec_is_invalid_measurements() =>
        Assert.Equal(
            ErrorCodes.InvalidMeasurements,
            Record([(_loading.Id, 20m), (_thickness.Id, 105m), (Guid.NewGuid(), 1m)]).Error!.Code);

    [Fact]
    public void Value_with_too_many_decimals_is_invalid_quantity() =>
        Assert.Equal(ErrorCodes.InvalidQuantity, Record([(_loading.Id, 20.12345m), (_thickness.Id, 105m)]).Error!.Code);

    [Fact]
    public void Value_with_four_decimals_is_accepted()
    {
        var density = TestData.CathodeSpec(OperationCode.Coat, "Density", "g/cm3", 3.35m, 3.55m, 3);

        var result = Record([(density.Id, 3.3505m)], specs: [density]);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.Pass, result.Value.Result);
    }

    [Fact]
    public void Negative_value_is_allowed_and_judged()
    {
        var vacuum = TestData.CathodeSpec(OperationCode.Coat, "Vacuum", "kPa", -95m, -85m, 3);

        var result = Record([(vacuum.Id, -90m)], specs: [vacuum]);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.Pass, result.Value.Result);
    }

    [Fact]
    public void Value_on_the_lsl_is_pass()
    {
        var result = Record([(_loading.Id, 19.5m), (_thickness.Id, 105m)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.Pass, result.Value.Result);
    }

    [Theory]
    [InlineData(99_999_999.999, true)]
    [InlineData(-99_999_999.999, true)]
    [InlineData(100_000_000, false)]
    [InlineData(-100_000_000, false)]
    [InlineData(999_999_999.999, false)]
    public void Value_must_fit_the_measurement_column(double value, bool fits)
    {
        var wide = TestData.CathodeSpec(OperationCode.Coat, "Wide", "u", -999_999_999m, 999_999_999m, 4);

        var result = Record([(wide.Id, (decimal)value)], specs: [wide]);

        if (fits)
        {
            Assert.True(result.IsSuccess);
        }
        else
        {
            Assert.Equal(ErrorCodes.InvalidQuantity, result.Error!.Code);
        }
    }

    [Fact]
    public void No_specs_is_no_inspection_spec() =>
        Assert.Equal(ErrorCodes.NoInspectionSpec, Record([], specs: []).Error!.Code);

    [Fact]
    public void Reject_over_lot_qty_is_qty_exceeds_lot() =>
        Assert.Equal(
            ErrorCodes.QtyExceedsLot,
            Record(OutOfLimits(), CoatDefect(), "Too heavy", _lot.Qty + 1).Error!.Code);

    [Fact]
    public void Negative_reject_is_invalid_quantity() =>
        Assert.Equal(ErrorCodes.InvalidQuantity, Record(OutOfLimits(), CoatDefect(), "Too heavy", -1m).Error!.Code);

    [Fact]
    public void Measurements_snapshot_limits()
    {
        var inspection = Record(InLimits()).Value;

        var loading = inspection.Measurements.Single(m => m.SpecId == _loading.Id);
        Assert.Equal(_loading.ItemName, loading.ItemName);
        Assert.Equal(_loading.Unit, loading.Unit);
        Assert.Equal(_loading.Lsl, loading.Lsl);
        Assert.Equal(_loading.Usl, loading.Usl);
        Assert.Equal(20m, loading.Value);

        _loading.UpdateLimits(10m, 30m);
        Assert.Equal(19.5m, loading.Lsl);
        Assert.Equal(20.5m, loading.Usl);
    }

    [Fact]
    public void Disposition_is_set_once_on_a_fail_inspection()
    {
        var inspection = Record(OutOfLimits(), CoatDefect(), "Too heavy").Value;
        var userId = Guid.NewGuid();

        var first = inspection.SetDisposition(Disposition.Scrap, userId, "Cannot rework", Now);
        var second = inspection.SetDisposition(Disposition.Release, userId, "Changed mind", Now);

        Assert.True(first.IsSuccess);
        Assert.Equal(Disposition.Scrap, inspection.Disposition);
        Assert.Equal(userId, inspection.DispositionById);
        Assert.Equal(Now, inspection.DispositionAt);
        Assert.Equal("Cannot rework", inspection.DispositionReason);
        Assert.Equal(ErrorCodes.LotNotAvailable, second.Error!.Code);
    }

    [Fact]
    public void Disposition_on_a_pass_inspection_is_lot_not_available() =>
        Assert.Equal(
            ErrorCodes.LotNotAvailable,
            Record(InLimits()).Value.SetDisposition(Disposition.Release, Guid.NewGuid(), "x", Now).Error!.Code);
}
