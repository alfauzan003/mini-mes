using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.UnitTests;

/// <summary>Builds domain objects for unit tests without a DbContext.</summary>
public static class TestData
{
    public static Product CathodeProduct() => Product.Create(
        "CATH-NCM811", "Cathode NCM811", Polarity.Cathode,
        OperationCode.Mix, OperationCode.Coat, OperationCode.Cal, OperationCode.Slit);

    public static Material CathodeFoil() => new("AL-FOIL", "Aluminium foil", LotType.Foil, Polarity.Cathode, "m");

    public static Material CathodeRaw() => new("NCM811", "NCM811 powder", LotType.Raw, Polarity.Cathode, "kg");

    public static Equipment Equipment(string code, OperationCode operation) => new(code, code, operation);

    /// <summary>Equipment already in a non-idle status; nothing in the domain sets MAINTENANCE yet.</summary>
    public static Equipment Equipment(string code, OperationCode operation, EquipmentStatus status)
    {
        var equipment = Equipment(code, operation);
        typeof(EquipmentEntity).GetProperty(nameof(EquipmentEntity.Status))!.SetValue(equipment, status);
        return equipment;
    }

    public static CarrierType BobbinType() => new("BB", "Bobbin", LotType.Electrode);

    public static CarrierType PancakeCoreType() => new("PC", "Pancake core", LotType.Pancake);

    public static Carrier Bobbin(string code = "BB-0001") => new(code, BobbinType());

    public static Carrier PancakeCore(string code = "PC-0001") => new(code, PancakeCoreType());

    public static Lot OutputLot(LotType type, string lotId = "EC-261002-0001") => Lot.CreateOutput(
        lotId, type, CathodeProduct(), Guid.NewGuid(), 1000, type == LotType.Pancake ? "pcs" : "m",
        OperationCode.Slit, Guid.NewGuid(), new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero));
}
