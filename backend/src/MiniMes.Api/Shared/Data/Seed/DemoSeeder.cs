using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.Quality.Domain;
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

    private const string CathodeCode = "CATH-NCM811";
    private const string AnodeCode = "ANOD-GRAPHITE";

    private sealed record Limits(decimal Lsl, decimal Usl);

    private sealed record SpecRow(OperationCode Operation, string Item, string Unit, Limits Cathode, Limits Anode);

    private static readonly SpecRow[] SpecSeed =
    [
        new(OperationCode.Mix, "Viscosity", "cP", new(4000m, 8000m), new(2000m, 4000m)),
        new(OperationCode.Mix, "Solid content", "%", new(68m, 72m), new(48m, 52m)),
        new(OperationCode.Coat, "Loading weight", "mg/cm²", new(19.5m, 20.5m), new(9.8m, 10.4m)),
        new(OperationCode.Cal, "Thickness", "µm", new(118m, 122m), new(128m, 132m)),
        new(OperationCode.Cal, "Density", "g/cm³", new(3.35m, 3.55m), new(1.55m, 1.70m)),
        new(OperationCode.Slit, "Width", "mm", new(99.8m, 100.2m), new(101.8m, 102.2m)),
        new(OperationCode.Slit, "Burr height", "µm", new(0m, 8m), new(0m, 8m))
    ];

    private static readonly (string Code, string Description, OperationCode? Operation)[] DefectCodeSeed =
    [
        ("MX-VISC", "Viscosity out of spec", OperationCode.Mix),
        ("MX-SOLID", "Solid content out of spec", OperationCode.Mix),
        ("MX-AGGL", "Agglomerates", OperationCode.Mix),
        ("CT-LOAD", "Loading weight out of spec", OperationCode.Coat),
        ("CT-PINHOLE", "Pinholes", OperationCode.Coat),
        ("CT-STREAK", "Streaks", OperationCode.Coat),
        ("CT-EDGE", "Edge bead", OperationCode.Coat),
        ("CP-THICK", "Thickness out of spec", OperationCode.Cal),
        ("CP-DENS", "Density out of spec", OperationCode.Cal),
        ("CP-WRINKLE", "Wrinkles", OperationCode.Cal),
        ("SL-WIDTH", "Width out of spec", OperationCode.Slit),
        ("SL-BURR", "Burr", OperationCode.Slit),
        ("SL-DUST", "Dust or particles", OperationCode.Slit),
        ("GEN-OTHER", "Other", null)
    ];

    private static readonly (OperationCode Operation, string Code, AlarmSeverity Severity, string Message)[] AlarmCodeSeed =
    [
        (OperationCode.Mix, "MX-TEMP-HIGH", AlarmSeverity.Major, "Slurry temperature high"),
        (OperationCode.Mix, "MX-VAC-LOW", AlarmSeverity.Warning, "Vacuum too weak"),
        (OperationCode.Mix, "MX-AGITATOR-FAULT", AlarmSeverity.Critical, "Agitator drive fault"),
        (OperationCode.Coat, "CT-TEMP-HIGH", AlarmSeverity.Major, "Dryer temperature high"),
        (OperationCode.Coat, "CT-DIE-PRESS-LOW", AlarmSeverity.Warning, "Slot-die pressure low"),
        (OperationCode.Coat, "CT-WEB-BREAK", AlarmSeverity.Critical, "Web break"),
        (OperationCode.Cal, "CP-TEMP-HIGH", AlarmSeverity.Major, "Roll temperature high"),
        (OperationCode.Cal, "CP-NIP-PRESS-HIGH", AlarmSeverity.Warning, "Nip pressure high"),
        (OperationCode.Cal, "CP-HYDRAULIC-FAULT", AlarmSeverity.Critical, "Hydraulic system fault"),
        (OperationCode.Slit, "SL-TEMP-HIGH", AlarmSeverity.Major, "Motor temperature high"),
        (OperationCode.Slit, "SL-TENSION-LOW", AlarmSeverity.Warning, "Web tension low"),
        (OperationCode.Slit, "SL-BLADE-FAULT", AlarmSeverity.Critical, "Slitting blade fault")
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        await SeedUsersAsync(ct);
        await SeedOperationsAsync(ct);
        await SeedProductsAsync(ct);
        await SeedMaterialsAsync(ct);
        await SeedEquipmentAsync(ct);
        await SeedCarriersAsync(ct);
        await SeedDefectCodesAsync(ct);
        await SeedAlarmCodesAsync(ct);
        await db.SaveChangesAsync(ct);
        if (configuration.GetValue<bool>("Seed:InspectionSpecs"))
        {
            await SeedInspectionSpecsAsync(ct);
        }

        await SeedMaterialLotsAsync(ct);
    }

    /// <summary>Spec limits for both demo products; skipped when any spec already exists.</summary>
    public async Task SeedInspectionSpecsAsync(CancellationToken ct)
    {
        if (await db.Set<InspectionSpec>().AnyAsync(ct))
        {
            return;
        }

        var products = await db.Set<Product>().ToDictionaryAsync(p => p.Code, p => p.Id, ct);
        var seqByOperation = new Dictionary<OperationCode, int>();
        foreach (var row in SpecSeed)
        {
            var seq = seqByOperation[row.Operation] = seqByOperation.GetValueOrDefault(row.Operation) + 1;
            db.Set<InspectionSpec>().Add(new InspectionSpec(
                products[CathodeCode], row.Operation, row.Item, row.Unit, row.Cathode.Lsl, row.Cathode.Usl, seq));
            db.Set<InspectionSpec>().Add(new InspectionSpec(
                products[AnodeCode], row.Operation, row.Item, row.Unit, row.Anode.Lsl, row.Anode.Usl, seq));
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedDefectCodesAsync(CancellationToken ct)
    {
        if (await db.Set<DefectCode>().AnyAsync(ct))
        {
            return;
        }

        db.Set<DefectCode>().AddRange(DefectCodeSeed.Select(d => new DefectCode(d.Code, d.Description, d.Operation)));
    }

    private async Task SeedAlarmCodesAsync(CancellationToken ct)
    {
        if (await db.Set<AlarmCode>().AnyAsync(ct))
        {
            return;
        }

        db.Set<AlarmCode>().AddRange(AlarmCodeSeed.Select(a => new AlarmCode(a.Code, a.Message, a.Severity, a.Operation)));
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
