using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.UnitTests.Execution;

public class OperationInputRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private static Lot Raw() => Lot.RegisterMaterial("RC-261002-001", TestData.CathodeRaw(), 500, Now);

    private static Lot Foil() => Lot.RegisterMaterial("FC-261002-001", TestData.CathodeFoil(), 6000, Now);

    private static Lot Slurry() => Output(LotType.Slurry, "SC-261002-001", OperationCode.Mix);

    private static Lot Electrode() => Output(LotType.Electrode, "EC-261002-0001", OperationCode.Coat);

    private static Lot Output(LotType type, string lotId, OperationCode producedBy) => Lot.CreateOutput(
        lotId, type, TestData.CathodeProduct(), Guid.NewGuid(), 100, "kg", producedBy, Guid.NewGuid(), Now);

    [Fact]
    public void Coat_needs_one_foil_and_one_slurry()
    {
        var foil = Foil();
        var slurry = Slurry();

        var result = OperationInputRules.Classify(OperationCode.Coat, [slurry, foil]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [(foil, RunInputRole.Primary), (slurry, RunInputRole.Secondary)],
            result.Value);
    }

    [Fact]
    public void Coat_with_foil_only_is_invalid_input_set()
    {
        var result = OperationInputRules.Classify(OperationCode.Coat, [Foil()]);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_INPUT_SET", result.Error!.Code);
    }

    [Fact]
    public void Mix_takes_raw_lots_as_secondary_inputs()
    {
        var first = Raw();
        var second = Raw();

        var result = OperationInputRules.Classify(OperationCode.Mix, [first, second]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [(first, RunInputRole.Secondary), (second, RunInputRole.Secondary)],
            result.Value);
    }

    [Fact]
    public void Mix_rejects_foil()
    {
        var result = OperationInputRules.Classify(OperationCode.Mix, [Raw(), Foil()]);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_INPUT_SET", result.Error!.Code);
    }

    [Fact]
    public void Mix_with_no_lots_is_invalid_input_set()
    {
        var result = OperationInputRules.Classify(OperationCode.Mix, []);

        Assert.Equal("INVALID_INPUT_SET", result.Error!.Code);
    }

    [Fact]
    public void Duplicate_lot_is_invalid_input_set()
    {
        var raw = Raw();

        var result = OperationInputRules.Classify(OperationCode.Mix, [raw, raw]);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_INPUT_SET", result.Error!.Code);
    }

    [Fact]
    public void Cal_takes_one_electrode_as_primary()
    {
        var electrode = Electrode();

        var result = OperationInputRules.Classify(OperationCode.Cal, [electrode]);

        Assert.True(result.IsSuccess);
        Assert.Equal([(electrode, RunInputRole.Primary)], result.Value);
    }

    [Fact]
    public void Slit_takes_one_electrode_as_primary()
    {
        var electrode = Electrode();

        var result = OperationInputRules.Classify(OperationCode.Slit, [electrode]);

        var only = Assert.Single(result.Value);
        Assert.Same(electrode, only.Lot);
        Assert.Equal(RunInputRole.Primary, only.Role);
    }

    [Fact]
    public void Slit_rejects_two_electrodes_and_wrong_types()
    {
        Assert.Equal(
            "INVALID_INPUT_SET",
            OperationInputRules.Classify(OperationCode.Slit, [Electrode(), Electrode()]).Error!.Code);
        Assert.Equal(
            "INVALID_INPUT_SET",
            OperationInputRules.Classify(OperationCode.Cal, [Slurry()]).Error!.Code);
    }
}
