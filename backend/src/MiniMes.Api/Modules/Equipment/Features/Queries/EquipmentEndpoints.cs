using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Features.Queries;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Features.Queries;

/// <param name="OpenRun">The run in progress on this equipment; null when none.</param>
public sealed record EquipmentDto(
    string Code, string Name, OperationCode Operation, int? LaneCount, EquipmentStatus Status, RunDto? OpenRun);

public static class EquipmentEndpoints
{
    public static void MapEquipmentQueries(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/equipment", async (
            string? operation, MesDbContext db, RunQueries runs, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParse<OperationCode>(operation, out var op))
            {
                return EnumQuery.Invalid(nameof(operation), operation);
            }

            var query = db.Set<EquipmentEntity>().AsNoTracking();
            if (op is { } filter)
            {
                query = query.Where(e => e.Operation == filter);
            }

            var items = await ToDtosAsync(query.OrderBy(e => e.Code), runs, ct);
            return TypedResults.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/equipment/{code}", async (
            string code, MesDbContext db, RunQueries runs, CancellationToken ct) =>
        {
            Result<EquipmentDto> result = await FindAsync(code, db, runs, ct)
                ?? (Result<EquipmentDto>)NotFound(code);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }

    public static Error NotFound(string code) =>
        new(ErrorCodes.EquipmentNotFound, $"Equipment '{code}' was not found.", ErrorKind.NotFound);

    public static async Task<EquipmentDto?> FindAsync(string code, MesDbContext db, RunQueries runs, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return (await ToDtosAsync(
            db.Set<EquipmentEntity>().AsNoTracking().Where(e => e.Code == normalized), runs, ct))
            .SingleOrDefault();
    }

    private static async Task<List<EquipmentDto>> ToDtosAsync(
        IQueryable<EquipmentEntity> equipment, RunQueries runs, CancellationToken ct)
    {
        var rows = await equipment
            .Select(e => new { e.Code, e.Name, e.Operation, e.LaneCount, e.Status, e.CurrentRunId })
            .ToListAsync(ct);
        var openRuns = await runs.GetManyAsync(
            rows.Where(r => r.CurrentRunId is not null).Select(r => r.CurrentRunId!.Value).ToList(), ct);

        return rows.Select(r => new EquipmentDto(
            r.Code, r.Name, r.Operation, r.LaneCount, r.Status,
            r.CurrentRunId is { } runId && openRuns.TryGetValue(runId, out var run) ? run : null)).ToList();
    }
}
