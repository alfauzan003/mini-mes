using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Carriers;

public class CarrierTests
{
    [Fact]
    public void Load_empty_bobbin_with_electrode_sets_full_and_lot_carrier()
    {
        var bobbin = TestData.Bobbin();
        var lot = TestData.OutputLot(LotType.Electrode);

        var result = bobbin.Load(lot);

        Assert.True(result.IsSuccess);
        Assert.Equal(CarrierStatus.Full, bobbin.Status);
        Assert.Equal(lot.Id, bobbin.CurrentLotId);
        Assert.Equal(bobbin.Id, lot.CurrentCarrierId);
    }

    [Fact]
    public void Load_full_carrier_is_carrier_not_empty()
    {
        var bobbin = TestData.Bobbin();
        bobbin.Load(TestData.OutputLot(LotType.Electrode, "EC-261002-0001"));
        var second = TestData.OutputLot(LotType.Electrode, "EC-261002-0002");

        var result = bobbin.Load(second);

        Assert.Equal(ErrorCodes.CarrierNotEmpty, result.Error?.Code);
        Assert.Null(second.CurrentCarrierId);
    }

    [Fact]
    public void Load_pancake_on_bobbin_is_type_mismatch()
    {
        var bobbin = TestData.Bobbin();
        var pancake = TestData.OutputLot(LotType.Pancake, "PC-261002-0001");

        var result = bobbin.Load(pancake);

        Assert.Equal(ErrorCodes.CarrierTypeMismatch, result.Error?.Code);
        Assert.Equal(CarrierStatus.Empty, bobbin.Status);
        Assert.Null(bobbin.CurrentLotId);
        Assert.Null(pancake.CurrentCarrierId);
    }

    [Fact]
    public void Unload_empties()
    {
        var bobbin = TestData.Bobbin();
        bobbin.Load(TestData.OutputLot(LotType.Electrode));

        bobbin.Unload();

        Assert.Equal(CarrierStatus.Empty, bobbin.Status);
        Assert.Null(bobbin.CurrentLotId);
    }
}
