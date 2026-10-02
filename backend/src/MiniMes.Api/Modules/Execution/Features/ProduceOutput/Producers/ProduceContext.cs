using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Quality;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Execution.Features.ProduceOutput.Producers;

/// <summary>Everything an operation's producer needs to turn output lines into lots, run outputs and events.</summary>
public sealed record ProduceContext(
    MesDbContext Db,
    LotIdGenerator Ids,
    IInspectionRequirement Inspection,
    ProductionRun Run,
    WorkOrder WorkOrder,
    Product Product,
    EquipmentEntity Equipment,
    IReadOnlyList<Lot> Inputs,
    Guid UserId,
    DateTimeOffset Now)
{
    public static string? NormalizeCarrierCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    public static Error InvalidSet(OperationCode operation, string reason) =>
        new(ErrorCodes.InvalidOutputSet, $"The outputs do not fit {operation}: {reason}.");

    /// <summary>The carrier for a typed or scanned code; its type is loaded because loading a lot checks it.</summary>
    public async Task<Result<Carrier>> FindCarrierAsync(string? code, CancellationToken ct)
    {
        var normalized = NormalizeCarrierCode(code);
        var carrier = normalized is null
            ? null
            : await Db.Set<Carrier>().Include(c => c.Type).SingleOrDefaultAsync(c => c.Code == normalized, ct);
        return carrier is not null
            ? carrier
            : new Error(ErrorCodes.CarrierNotFound, $"Carrier '{code}' was not found.", ErrorKind.NotFound);
    }

    /// <summary>Creates a lot made by the run, records which input lots it came from and writes its CREATE event.</summary>
    public Lot AddOutputLot(
        string lotId, LotType type, string uom, OperationCode producedBy, decimal qty, IEnumerable<Lot> parents)
    {
        var lot = Lot.CreateOutput(lotId, type, Product, WorkOrder.Id, qty, uom, producedBy, Equipment.Id, Now);
        Db.Set<Lot>().Add(lot);
        foreach (var parent in parents)
        {
            Db.Set<GenealogyLink>().Add(new GenealogyLink(parent.Id, lot.Id, Run.Id));
        }

        Record(lot, LotEventType.Create, qty);
        return lot;
    }

    /// <summary>Puts the lot on the carrier and writes the CARRIER_LOAD event.</summary>
    public Result Load(Carrier carrier, Lot lot, decimal qty)
    {
        var loaded = carrier.Load(lot);
        if (loaded.IsSuccess)
        {
            Record(lot, LotEventType.CarrierLoad, qty);
        }

        return loaded;
    }

    public void Record(Lot lot, LotEventType type, decimal? qty = null) =>
        Db.Set<LotEvent>().Add(LotEvent.Record(lot, type, UserId, Now, Run.Id, qty));
}
