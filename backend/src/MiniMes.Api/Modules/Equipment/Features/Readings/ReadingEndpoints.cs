using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Equipment.Parameters;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Features.Readings;

public sealed record ReadingPointDto(DateTimeOffset At, decimal Value);

public sealed record ParameterSeriesDto(
    string Parameter,
    ParameterKind Kind,
    string Unit,
    decimal Low,
    decimal High,
    IReadOnlyList<ReadingPointDto> Points);

public static class ReadingEndpoints
{
    public static readonly TimeSpan DefaultRange = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxRange = TimeSpan.FromHours(24);

    public static void MapReadings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/readings/latest", (LatestReadings latest) => TypedResults.Ok(latest.All()))
            .RequireAuthorization();

        app.MapGet("/api/equipment/{code}/parameters", async (
            string code, DateTimeOffset? from, DateTimeOffset? to,
            MesDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var end = to ?? time.GetUtcNow();
            var start = from ?? end - DefaultRange;
            if (start >= end || end - start > MaxRange)
            {
                return ((Result)new Error(
                    ErrorCodes.InvalidDateRange,
                    "from must be before to, and the range at most 24 hours.")).ToHttpResult();
            }

            var normalized = code.Trim().ToUpperInvariant();
            var equipment = await db.Set<EquipmentEntity>().AsNoTracking()
                .Where(e => e.Code == normalized)
                .Select(e => new { e.Id, e.Operation })
                .SingleOrDefaultAsync(ct);
            if (equipment is null)
            {
                return ((Result)EquipmentEndpoints.NotFound(code)).ToHttpResult();
            }

            var definitions = await db.Set<ParameterDefinition>().AsNoTracking()
                .Where(d => d.Operation == equipment.Operation)
                .OrderBy(d => d.Seq)
                .ToListAsync(ct);
            var points = await db.Set<ParameterReading>().AsNoTracking()
                .Where(r => r.EquipmentId == equipment.Id && r.RecordedAt >= start && r.RecordedAt <= end)
                .OrderBy(r => r.RecordedAt).ThenBy(r => r.Id)
                .Select(r => new { r.Parameter, r.RecordedAt, r.Value })
                .ToListAsync(ct);
            var byParameter = points.ToLookup(p => p.Parameter, p => new ReadingPointDto(p.RecordedAt, p.Value));

            return TypedResults.Ok(definitions.Select(d => new ParameterSeriesDto(
                d.Name, d.Kind, d.Unit, d.Low, d.High, [.. byParameter[d.Name]])).ToList());
        }).RequireAuthorization();
    }
}
