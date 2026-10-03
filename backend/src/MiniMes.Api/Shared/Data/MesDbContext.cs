using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Shared.Data;

public class MesDbContext(DbContextOptions<MesDbContext> options, TimeProvider time) : DbContext(options)
{
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        WriteEquipmentStatusLog();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void WriteEquipmentStatusLog()
    {
        var now = time.GetUtcNow();
        foreach (var equipment in ChangeTracker.Entries<EquipmentEntity>().Select(e => e.Entity).ToList())
        {
            foreach (var change in equipment.PendingStatusChanges)
            {
                Set<EquipmentStatusLog>().Add(new EquipmentStatusLog(equipment.Id, change, now));
            }

            equipment.ClearPendingStatusChanges();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MesDbContext).Assembly);

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct)
    {
        Result<T> result = default;
        var error = await RunInTransactionAsync(async token =>
        {
            result = await work(token);
            return result.Error;
        }, ct);

        return error is null ? result : error;
    }

    public async Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> work, CancellationToken ct)
    {
        var error = await RunInTransactionAsync(async token => (await work(token)).Error, ct);
        return error is null ? Result.Success() : error;
    }

    private async Task<Error?> RunInTransactionAsync(Func<CancellationToken, Task<Error?>> work, CancellationToken ct)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            var error = await work(ct);
            if (error is not null)
            {
                await transaction.RollbackAsync(ct);
                ChangeTracker.Clear();
                return error;
            }

            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }
        catch
        {
            ChangeTracker.Clear();
            throw;
        }
    }
}
