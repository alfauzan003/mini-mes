using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.UnitTests;

/// <summary>Builds domain objects for unit tests without a DbContext.</summary>
public static class TestData
{
    public static Product CathodeProduct() => Product.Create(
        "CATH-NCM811", "Cathode NCM811", Polarity.Cathode,
        OperationCode.Mix, OperationCode.Coat, OperationCode.Cal, OperationCode.Slit);

    public static Material CathodeFoil() => new("AL-FOIL", "Aluminium foil", LotType.Foil, Polarity.Cathode, "m");

    public static Material CathodeRaw() => new("NCM811", "NCM811 powder", LotType.Raw, Polarity.Cathode, "kg");
}
