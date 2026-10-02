using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Shared.Data.Seed;

public class DemoSeeder(MesDbContext db, IConfiguration configuration, LotIdGenerator lotIds, TimeProvider time)
{
    private const int SlitterLaneCount = 8;
    private const decimal RawLotQty = 500m;
    private const decimal FoilLotQty = 6000m;
    private const string AdminUsername = "admin";

    private static readonly (string Username, string DisplayName, Role Role)[] DemoUsers =
    [
        ("planner", "Demo Planner", Role.Planner),
        ("operator", "Demo Operator", Role.Operator),
        ("qc", "Demo QC", Role.QC),
        ("admin", "Demo Admin", Role.Admin)
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        await SeedUsersAsync(ct);
        await SeedOperationsAsync(ct);
        await SeedProductsAsync(ct);
        await SeedMaterialsAsync(ct);
        await SeedEquipmentAsync(ct);
        await SeedCarriersAsync(ct);
        await db.SaveChangesAsync(ct);
        await SeedMaterialLotsAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("Seed:DemoPassword must be set to seed demo users.");
        }

        var existing = await db.Set<User>().Select(u => u.Username).ToListAsync(ct);
        var hasher = new PasswordHasher<User>();
        foreach (var (username, displayName, role) in DemoUsers.Where(u => !existing.Contains(u.Username)))
        {
            var user = new User { Username = username, DisplayName = displayName, Role = role };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Set<User>().Add(user);
        }
    }

    private async Task SeedOperationsAsync(CancellationToken ct)
    {
        if (await db.Set<Operation>().AnyAsync(ct))
        {
            return;
        }

        db.Set<Operation>().AddRange(
            new Operation(OperationCode.Mix, "Mixing", LotType.Slurry, "kg"),
            new Operation(OperationCode.Coat, "Coating", LotType.Electrode, "m"),
            new Operation(OperationCode.Cal, "Calendering", LotType.Electrode, "m"),
            new Operation(OperationCode.Slit, "Slitting", LotType.Pancake, "m"));
    }

    private async Task SeedProductsAsync(CancellationToken ct)
    {
        if (await db.Set<Product>().AnyAsync(ct))
        {
            return;
        }

        OperationCode[] route = [OperationCode.Mix, OperationCode.Coat, OperationCode.Cal, OperationCode.Slit];
        db.Set<Product>().AddRange(
            Product.Create("CATH-NCM811", "Cathode NCM811", Polarity.Cathode, route),
            Product.Create("ANOD-GRAPHITE", "Anode Graphite", Polarity.Anode, route));
    }

    private async Task SeedMaterialsAsync(CancellationToken ct)
    {
        if (await db.Set<Material>().AnyAsync(ct))
        {
            return;
        }

        db.Set<Material>().AddRange(
            new Material("NCM811", "NCM811 cathode active material", LotType.Raw, Polarity.Cathode, "kg"),
            new Material("PVDF", "PVDF binder", LotType.Raw, Polarity.Cathode, "kg"),
            new Material("SUPER-P", "Super P conductive carbon", LotType.Raw, Polarity.Cathode, "kg"),
            new Material("NMP", "NMP solvent", LotType.Raw, Polarity.Cathode, "kg"),
            new Material("AL-FOIL", "Aluminium foil 15 µm", LotType.Foil, Polarity.Cathode, "m"),
            new Material("GRAPHITE", "Graphite anode active material", LotType.Raw, Polarity.Anode, "kg"),
            new Material("CMC", "CMC binder", LotType.Raw, Polarity.Anode, "kg"),
            new Material("SBR", "SBR binder", LotType.Raw, Polarity.Anode, "kg"),
            new Material("CU-FOIL", "Copper foil 8 µm", LotType.Foil, Polarity.Anode, "m"));
    }

    private async Task SeedEquipmentAsync(CancellationToken ct)
    {
        if (await db.Set<EquipmentEntity>().AnyAsync(ct))
        {
            return;
        }

        db.Set<EquipmentEntity>().AddRange(
            new EquipmentEntity("MX01", "Mixer 1", OperationCode.Mix),
            new EquipmentEntity("MX02", "Mixer 2", OperationCode.Mix),
            new EquipmentEntity("CT01", "Coater 1", OperationCode.Coat),
            new EquipmentEntity("CT02", "Coater 2", OperationCode.Coat),
            new EquipmentEntity("CP01", "Calender 1", OperationCode.Cal),
            new EquipmentEntity("CP02", "Calender 2", OperationCode.Cal),
            new EquipmentEntity("SL01", "Slitter 1", OperationCode.Slit, laneCount: SlitterLaneCount),
            new EquipmentEntity("SL02", "Slitter 2", OperationCode.Slit, laneCount: SlitterLaneCount));
    }

    private async Task SeedCarriersAsync(CancellationToken ct)
    {
        var types = await db.Set<CarrierType>().ToDictionaryAsync(t => t.Code, ct);
        if (types.Count == 0)
        {
            foreach (var type in new[]
            {
                new CarrierType("BB", "Bobbin", LotType.Electrode),
                new CarrierType("PC", "Pancake core", LotType.Pancake)
            })
            {
                db.Set<CarrierType>().Add(type);
                types[type.Code] = type;
            }
        }

        if (await db.Set<Carrier>().AnyAsync(ct))
        {
            return;
        }

        AddCarriers(types["BB"], 40);
        AddCarriers(types["PC"], 200);
    }

    /// <summary>One received lot per material, registered by the demo admin.</summary>
    private async Task SeedMaterialLotsAsync(CancellationToken ct)
    {
        if (await db.Set<Lot>().AnyAsync(ct))
        {
            return;
        }

        var adminId = await db.Set<User>().Where(u => u.Username == AdminUsername).Select(u => u.Id).SingleAsync(ct);
        var materials = await db.Set<Material>().OrderBy(m => m.Code).ToListAsync(ct);

        var result = await db.ExecuteInTransactionAsync(async token =>
        {
            foreach (var material in materials)
            {
                var lotId = await lotIds.NextLotIdAsync(material.Kind, material.Polarity, null, token);
                if (!lotId.IsSuccess)
                {
                    return lotId.Error!;
                }

                var now = time.GetUtcNow();
                var qty = material.Kind == LotType.Foil ? FoilLotQty : RawLotQty;
                var lot = Lot.RegisterMaterial(lotId.Value, material, qty, now);
                db.Set<Lot>().Add(lot);
                db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Register, adminId, now, qty: qty));
            }

            return Result.Success();
        }, ct);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Could not seed material lots: {result.Error!.Code}.");
        }
    }

    private void AddCarriers(CarrierType type, int count) =>
        db.Set<Carrier>().AddRange(Enumerable.Range(1, count)
            .Select(n => new Carrier($"{type.Code}-{n:D4}", type)));
}
