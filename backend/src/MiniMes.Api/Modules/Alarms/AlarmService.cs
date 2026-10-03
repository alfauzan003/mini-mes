using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using Npgsql;
using AlarmCodeEntity = MiniMes.Api.Modules.Alarms.Domain.AlarmCode;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Alarms;

/// <summary>Raises and clears equipment alarms; a CRITICAL alarm takes the equipment DOWN until the last one clears.</summary>
public class AlarmService(MesDbContext db, TimeProvider time)
{
    private const string UniqueViolation = "23505";
    private const int MaxAttempts = 3;

    /// <summary>Raises the alarm, or returns the ID of the one already active for that equipment and code.</summary>
    public async Task<Result<Guid>> RaiseAsync(string equipmentCode, string alarmCode, CancellationToken ct)
    {
        try
        {
            return await RetryOnConcurrencyAsync(
                () => db.ExecuteInTransactionAsync(token => RaiseCoreAsync(equipmentCode, alarmCode, token), ct));
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException
            { InnerException: PostgresException { SqlState: UniqueViolation } })
        {
            // A concurrent raise of the same alarm won the race; its alarm is the one to report.
            var existing = await FindActiveAsync(equipmentCode, alarmCode, ct);
            if (existing is null)
            {
                throw;
            }

            return existing.Id;
        }
    }

    public async Task<Result> ClearAsync(string equipmentCode, string alarmCode, CancellationToken ct) =>
        await RetryOnConcurrencyAsync(() => ClearOnceAsync(equipmentCode, alarmCode, ct));

    /// <summary>
    /// An operator ack or a track-out can commit between our read and write; the failed transaction left a clean
    /// change tracker, so running the work again reloads fresh rows and decides on what is now true.
    /// </summary>
    private static async Task<T> RetryOnConcurrencyAsync<T>(Func<Task<T>> attempt)
    {
        for (var tries = 1; ; tries++)
        {
            try
            {
                return await attempt();
            }
            catch (DbUpdateConcurrencyException) when (tries < MaxAttempts)
            {
            }
        }
    }

    private async Task<Result> ClearOnceAsync(string equipmentCode, string alarmCode, CancellationToken ct) =>
        await db.ExecuteInTransactionAsync(async token =>
        {
            var equipment = await db.Set<EquipmentEntity>().SingleOrDefaultAsync(e => e.Code == equipmentCode, token);
            if (equipment is null)
            {
                return EquipmentNotFound(equipmentCode);
            }

            var alarm = await db.Set<Alarm>().SingleOrDefaultAsync(
                a => a.EquipmentId == equipment.Id && a.AlarmCode == alarmCode && a.ClearedAt == null, token);
            if (alarm is null)
            {
                return Result.Success();
            }

            var cleared = alarm.Clear(time.GetUtcNow());
            if (!cleared.IsSuccess)
            {
                return cleared;
            }

            if (equipment.Status == EquipmentStatus.Down)
            {
                var otherCritical = await db.Set<Alarm>().AnyAsync(
                    a => a.EquipmentId == equipment.Id && a.ClearedAt == null && a.Id != alarm.Id
                        && a.Severity == AlarmSeverity.Critical, token);
                if (!otherCritical)
                {
                    equipment.RecoverFromDown($"Alarm {alarmCode} cleared");
                }
            }

            return Result.Success();
        }, ct);

    private async Task<Result<Guid>> RaiseCoreAsync(string equipmentCode, string alarmCode, CancellationToken ct)
    {
        var equipment = await db.Set<EquipmentEntity>().SingleOrDefaultAsync(e => e.Code == equipmentCode, ct);
        if (equipment is null)
        {
            return EquipmentNotFound(equipmentCode);
        }

        var code = await db.Set<AlarmCodeEntity>().SingleOrDefaultAsync(c => c.Code == alarmCode, ct);
        if (code is null || code.Operation != equipment.Operation)
        {
            return new Error(
                ErrorCodes.AlarmCodeNotFound, $"Alarm code {alarmCode} does not exist for {equipment.Operation}.");
        }

        var existing = await db.Set<Alarm>().AsNoTracking().SingleOrDefaultAsync(
            a => a.EquipmentId == equipment.Id && a.AlarmCode == alarmCode && a.ClearedAt == null, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var alarm = Alarm.Raise(equipment.Id, code, time.GetUtcNow());
        db.Set<Alarm>().Add(alarm);
        if (code.Severity == AlarmSeverity.Critical)
        {
            equipment.GoDown(code.Code);
        }

        return alarm.Id;
    }

    private async Task<Alarm?> FindActiveAsync(string equipmentCode, string alarmCode, CancellationToken ct) =>
        await db.Set<Alarm>().AsNoTracking().SingleOrDefaultAsync(
            a => a.AlarmCode == alarmCode && a.ClearedAt == null
                && db.Set<EquipmentEntity>().Any(e => e.Id == a.EquipmentId && e.Code == equipmentCode), ct);

    private static Error EquipmentNotFound(string code) =>
        new(ErrorCodes.EquipmentNotFound, $"Equipment {code} does not exist.");
}
