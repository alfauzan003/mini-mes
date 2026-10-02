using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Shared.Data;

public class MesDbContext(DbContextOptions<MesDbContext> options) : DbContext(options)
{
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
