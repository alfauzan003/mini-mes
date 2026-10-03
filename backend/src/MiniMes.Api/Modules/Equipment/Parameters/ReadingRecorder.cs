using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Quantities;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Parameters;

public sealed record ReadingInput(string Parameter, decimal Value);

public sealed class ReadingRecorder(
    MesDbContext db, LatestReadings latest, TimeProvider time, IOptions<ParametersOptions> options)
{
    /// <summary>
    /// Validates the readings, publishes them as the latest values and stores those that are due.
    /// Nothing is published or stored when any reading is rejected.
    /// </summary>
    public async Task<Result<IReadOnlyList<LiveReadingDto>>> RecordAsync(
        string equipmentCode, IReadOnlyList<ReadingInput> readings, CancellationToken ct)
    {
        var normalized = equipmentCode.Trim().ToUpperInvariant();
        var equipment = await db.Set<EquipmentEntity>().AsNoTracking()
            .Where(e => e.Code == normalized)
            .Select(e => new { e.Id, e.Code, e.Operation })
            .SingleOrDefaultAsync(ct);
        if (equipment is null)
        {
            return EquipmentEndpoints.NotFound(equipmentCode);
        }

        var definitions = await db.Set<ParameterDefinition>().AsNoTracking()
            .Where(d => d.Operation == equipment.Operation)
            .ToDictionaryAsync(d => d.Name, ct);

        foreach (var input in readings)
        {
            if (!definitions.ContainsKey(input.Parameter))
            {
                return new Error(
                    ErrorCodes.UnknownParameter,
                    $"Equipment {equipment.Code} has no parameter '{input.Parameter}'.");
            }

            if (input.Value < -QuantityRules.Max || !QuantityRules.Fits(input.Value))
            {
                return new Error(
                    ErrorCodes.InvalidQuantity,
                    $"Reading '{input.Parameter}' has at most {QuantityRules.Decimals} decimals " +
                    $"and must be within +/-{QuantityRules.Max:N3}.");
            }
        }

        var now = time.GetUtcNow();
        var interval = TimeSpan.FromSeconds(options.Value.PersistIntervalSeconds);
        var live = new List<LiveReadingDto>(readings.Count);
        foreach (var input in readings)
        {
            var definition = definitions[input.Parameter];
            var dto = new LiveReadingDto(
                equipment.Code, definition.Name, definition.Kind, definition.Unit,
                input.Value, definition.Low, definition.High, now);
            latest.Update(dto);
            live.Add(dto);

            if (latest.TryMarkPersisted(equipment.Code, definition.Name, now, interval))
            {
                db.Set<ParameterReading>().Add(new ParameterReading(equipment.Id, definition.Name, input.Value, now));
            }
        }

        await db.SaveChangesAsync(ct);
        return live;
    }
}
