using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Lots;

public class LotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly Product Cathode = TestData.CathodeProduct();

    private static Lot Electrode(OperationCode producedBy = OperationCode.Coat, decimal qty = 6000) =>
        Lot.CreateOutput("EC-261002-0001", LotType.Electrode, Cathode, Guid.NewGuid(), qty, "m",
            producedBy, Guid.NewGuid(), Now);

    private static Lot Slurry(decimal qty = 6000) =>
        Lot.CreateOutput("SC-261002-0001", LotType.Slurry, Cathode, Guid.NewGuid(), qty, "kg",
            OperationCode.Mix, Guid.NewGuid(), Now);

    [Fact]
    public void Registered_foil_is_waiting_pass_and_next_coat()
    {
        var material = TestData.CathodeFoil();

        var lot = Lot.RegisterMaterial("FC-261002-0001", material, 6000, Now);

        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(QualityStatus.Pass, lot.Quality);
        Assert.Equal(OperationCode.Coat, lot.NextOperation);
        Assert.Null(lot.CurrentOperation);
        Assert.Equal(LotType.Foil, lot.Type);
        Assert.Equal(material.Id, lot.MaterialId);
        Assert.Equal(6000, lot.Qty);
        Assert.Equal("m", lot.Uom);
    }

    [Fact]
    public void Registered_raw_material_points_to_mix()
    {
        var lot = Lot.RegisterMaterial("RC-261002-0001", TestData.CathodeRaw(), 500, Now);

        Assert.Equal(OperationCode.Mix, lot.NextOperation);
    }

    [Fact]
    public void Output_lot_starts_wait_none_with_next_operation_from_route()
    {
        var equipmentId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();

        var lot = Lot.CreateOutput("EC-261002-0001", LotType.Electrode, Cathode, workOrderId, 6000, "m",
            OperationCode.Coat, equipmentId, Now);

        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(QualityStatus.None, lot.Quality);
        Assert.Equal(OperationCode.Coat, lot.CurrentOperation);
        Assert.Equal(OperationCode.Cal, lot.NextOperation);
        Assert.Equal(Cathode.Id, lot.ProductId);
        Assert.Equal(workOrderId, lot.WorkOrderId);
        Assert.Equal(equipmentId, lot.CurrentEquipmentId);
    }

    [Fact]
    public void Track_in_requires_wait()
    {
        var lot = Electrode();
        var equipmentId = Guid.NewGuid();

        var first = lot.TrackIn(equipmentId);
        var second = lot.TrackIn(Guid.NewGuid());

        Assert.True(first.IsSuccess);
        Assert.Equal(LotStatus.Run, lot.Status);
        Assert.Equal(equipmentId, lot.CurrentEquipmentId);
        Assert.Equal(ErrorCodes.LotNotAvailable, second.Error!.Code);
    }

    [Fact]
    public void Full_consumption_marks_consumed_and_clears_carrier()
    {
        var lot = Slurry(6000);
        lot.PlaceOnCarrier(Guid.NewGuid());
        lot.TrackIn(Guid.NewGuid());

        var result = lot.Consume(6000);

        Assert.True(result.IsSuccess);
        Assert.Equal(LotStatus.Consumed, lot.Status);
        Assert.Equal(0, lot.Qty);
        Assert.Null(lot.CurrentCarrierId);
    }

    [Fact]
    public void Partial_consumption_returns_to_wait_with_remaining_qty()
    {
        var lot = Lot.RegisterMaterial("FC-261002-0001", TestData.CathodeFoil(), 6000, Now);
        lot.TrackIn(Guid.NewGuid());

        var result = lot.Consume(2000);

        Assert.True(result.IsSuccess);
        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(4000, lot.Qty);
    }

    [Fact]
    public void Consuming_when_not_running_is_lot_not_available()
    {
        var lot = Slurry();

        Assert.Equal(ErrorCodes.LotNotAvailable, lot.Consume(10).Error!.Code);
    }

    [Fact]
    public void Consuming_more_than_qty_is_qty_exceeds_lot()
    {
        var lot = Slurry(100);
        lot.TrackIn(Guid.NewGuid());

        var result = lot.Consume(100.001m);

        Assert.Equal(ErrorCodes.QtyExceedsLot, result.Error!.Code);
        Assert.Equal(100, lot.Qty);
        Assert.Equal(LotStatus.Run, lot.Status);
    }

    [Fact]
    public void Negative_consumption_is_invalid_quantity()
    {
        var lot = Slurry(100);
        lot.TrackIn(Guid.NewGuid());

        var result = lot.Consume(-1);

        Assert.Equal(ErrorCodes.InvalidQuantity, result.Error!.Code);
        Assert.Equal(100, lot.Qty);
    }

    [Fact]
    public void Returning_a_running_lot_to_wait_changes_nothing_else()
    {
        var lot = Electrode(OperationCode.Cal, 1180);
        var carrierId = Guid.NewGuid();
        lot.PlaceOnCarrier(carrierId);
        lot.TrackIn(Guid.NewGuid());

        var result = lot.ReturnToWait();

        Assert.True(result.IsSuccess);
        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(1180, lot.Qty);
        Assert.Equal(carrierId, lot.CurrentCarrierId);
        Assert.Equal(OperationCode.Slit, lot.NextOperation);
    }

    [Fact]
    public void Returning_a_lot_that_is_not_running_to_wait_is_lot_not_available()
    {
        var lot = Electrode();

        Assert.Equal(ErrorCodes.LotNotAvailable, lot.ReturnToWait().Error!.Code);
    }

    [Fact]
    public void Calendering_keeps_lot_resets_quality_and_points_to_slit()
    {
        var lot = Electrode(OperationCode.Coat, 6000);
        lot.TrackIn(Guid.NewGuid());

        var result = lot.CompleteCalendering(5800, Cathode);

        Assert.True(result.IsSuccess);
        Assert.Equal(LotStatus.Wait, lot.Status);
        Assert.Equal(5800, lot.Qty);
        Assert.Equal(QualityStatus.None, lot.Quality);
        Assert.Equal(OperationCode.Cal, lot.CurrentOperation);
        Assert.Equal(OperationCode.Slit, lot.NextOperation);
    }

    [Fact]
    public void Calendering_requires_run()
    {
        var lot = Electrode();

        Assert.Equal(ErrorCodes.LotNotAvailable, lot.CompleteCalendering(5800, Cathode).Error!.Code);
    }

    [Fact]
    public void Finish_requires_waiting_pancake()
    {
        var electrode = Electrode(OperationCode.Cal);
        var pancake = Lot.CreateOutput("PC-261002-0001", LotType.Pancake, Cathode, Guid.NewGuid(), 500, "m",
            OperationCode.Slit, Guid.NewGuid(), Now);

        var wrongType = electrode.Finish();
        var finished = pancake.Finish();

        Assert.Equal(ErrorCodes.LotNotAvailable, wrongType.Error!.Code);
        Assert.True(finished.IsSuccess);
        Assert.Equal(LotStatus.Finished, pancake.Status);
        Assert.Null(pancake.NextOperation);
        Assert.Equal(ErrorCodes.LotNotAvailable, pancake.Finish().Error!.Code);
    }

    [Fact]
    public void Recorded_event_copies_lot_state()
    {
        var lot = Electrode();
        var carrierId = Guid.NewGuid();
        lot.PlaceOnCarrier(carrierId);
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var ev = LotEvent.Record(lot, LotEventType.TrackIn, userId, Now, runId, 10m, "note");

        Assert.Equal(lot.Id, ev.LotId);
        Assert.Equal(LotEventType.TrackIn, ev.Type);
        Assert.Equal(OperationCode.Coat, ev.Operation);
        Assert.Equal(lot.CurrentEquipmentId, ev.EquipmentId);
        Assert.Equal(carrierId, ev.CarrierId);
        Assert.Equal(runId, ev.RunId);
        Assert.Equal(userId, ev.UserId);
        Assert.Equal(10m, ev.Qty);
        Assert.Equal("note", ev.Note);
        Assert.Equal(Now, ev.OccurredAt);
    }

    [Fact]
    public void Recorded_event_can_override_the_operation()
    {
        var lot = Electrode();

        var ev = LotEvent.Record(
            lot, LotEventType.TrackOut, Guid.NewGuid(), Now, operation: OperationCode.Slit);

        Assert.Equal(OperationCode.Slit, ev.Operation);
    }
}
