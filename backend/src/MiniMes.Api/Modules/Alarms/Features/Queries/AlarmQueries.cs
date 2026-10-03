using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Data;
using AlarmCodeEntity = MiniMes.Api.Modules.Alarms.Domain.AlarmCode;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Alarms.Features.Queries;

public sealed record AlarmFilter(
    bool? Active, string? Equipment, AlarmSeverity? Severity, DateTimeOffset? From, DateTimeOffset? To);

public sealed class AlarmQueries(MesDbContext db)
{
    public const int MaxListSize = 500;

    /// <summary>Alarms matching the filter on their raise time, newest first, capped at <see cref="MaxListSize"/>.</summary>
    public async Task<IReadOnlyList<AlarmDto>> ListAsync(AlarmFilter f, CancellationToken ct)
    {
        var query = db.Set<Alarm>().AsNoTracking().AsQueryable();
        if (f.Active is { } active)
        {
            query = active ? query.Where(a => a.ClearedAt == null) : query.Where(a => a.ClearedAt != null);
        }

        if (!string.IsNullOrWhiteSpace(f.Equipment))
        {
            var code = f.Equipment.Trim().ToUpperInvariant();
            query = query.Where(a => db.Set<EquipmentEntity>().Any(e => e.Id == a.EquipmentId && e.Code == code));
        }

        if (f.Severity is { } severity)
        {
            query = query.Where(a => a.Severity == severity);
        }

        if (f.From is { } from)
        {
            query = query.Where(a => a.RaisedAt >= from);
        }

        if (f.To is { } to)
        {
            query = query.Where(a => a.RaisedAt <= to);
        }

        var ids = await query
            .OrderByDescending(a => a.RaisedAt).ThenBy(a => a.Id)
            .Select(a => a.Id)
            .Take(MaxListSize)
            .ToListAsync(ct);
        return await LoadManyAsync(db, ids, ct);
    }

    public async Task<AlarmDto?> GetAsync(Guid id, CancellationToken ct) =>
        (await LoadManyAsync(db, [id], ct)).SingleOrDefault();

    /// <summary>Loads the alarms with the given IDs, newest first. Static so the change feed can call it with its own context.</summary>
    public static async Task<IReadOnlyList<AlarmDto>> LoadManyAsync(
        MesDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.Set<Alarm>().AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Join(
                db.Set<EquipmentEntity>().AsNoTracking(),
                a => a.EquipmentId, e => e.Id,
                (a, e) => new { Alarm = a, EquipmentCode = e.Code })
            .Join(
                db.Set<AlarmCodeEntity>().AsNoTracking(),
                x => x.Alarm.AlarmCode, c => c.Code,
                (x, c) => new { x.Alarm, x.EquipmentCode, c.Message })
            .OrderByDescending(x => x.Alarm.RaisedAt).ThenBy(x => x.Alarm.Id)
            .ToListAsync(ct);

        var userIds = rows
            .Where(r => r.Alarm.AcknowledgedById is not null)
            .Select(r => r.Alarm.AcknowledgedById!.Value)
            .Distinct()
            .ToArray();
        var usernames = userIds.Length == 0
            ? []
            : await db.Set<User>().AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        return [.. rows.Select(r => new AlarmDto(
            r.Alarm.Id,
            r.EquipmentCode,
            r.Alarm.AlarmCode,
            r.Message,
            r.Alarm.Severity,
            r.Alarm.RaisedAt,
            r.Alarm.ClearedAt,
            r.Alarm.ClearedAt is { } cleared ? (cleared - r.Alarm.RaisedAt).TotalSeconds : null,
            r.Alarm.AcknowledgedById is { } by ? usernames.GetValueOrDefault(by) : null,
            r.Alarm.AcknowledgedAt))];
    }
}
