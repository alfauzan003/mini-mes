using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Quality;

public class InspectionSpecTests
{
    private readonly InspectionSpec _loading =
        new(Guid.NewGuid(), OperationCode.Coat, "Loading weight", "mg/cm²", 19.5m, 20.5m, 1);

    [Theory]
    [InlineData(19.5, Judgment.Ok)]
    [InlineData(20.5, Judgment.Ok)]
    [InlineData(19.49, Judgment.Ng)]
    [InlineData(20.51, Judgment.Ng)]
    public void Judges_inclusive_limits(decimal value, Judgment expected) =>
        Assert.Equal(expected, _loading.Judge(value));

    [Fact]
    public void Lsl_not_below_usl_is_invalid_spec_limits() =>
        Assert.Equal(ErrorCodes.InvalidSpecLimits, _loading.UpdateLimits(21m, 20m).Error!.Code);

    [Fact]
    public void Equal_limits_are_invalid_spec_limits() =>
        Assert.Equal(ErrorCodes.InvalidSpecLimits, _loading.UpdateLimits(20m, 20m).Error!.Code);

    [Fact]
    public void Valid_limits_update_the_spec()
    {
        var result = _loading.UpdateLimits(19m, 21m);

        Assert.True(result.IsSuccess);
        Assert.Equal(19m, _loading.Lsl);
        Assert.Equal(21m, _loading.Usl);
    }

    [Fact]
    public void Constructor_rejects_lsl_not_below_usl() =>
        Assert.Throws<ArgumentException>(() =>
            new InspectionSpec(Guid.NewGuid(), OperationCode.Coat, "Loading weight", "mg/cm²", 20m, 20m, 1));

    [Fact]
    public void Defect_code_with_operation_applies_only_to_it()
    {
        var burr = new DefectCode("SL-BURR", "Slit burr", OperationCode.Slit);

        Assert.True(burr.AppliesTo(OperationCode.Slit));
        Assert.False(burr.AppliesTo(OperationCode.Coat));
    }

    [Fact]
    public void Defect_code_without_operation_applies_to_every_operation()
    {
        var other = new DefectCode("GEN-OTHER", "Other", null);

        Assert.All(Enum.GetValues<OperationCode>(), op => Assert.True(other.AppliesTo(op)));
    }
}
