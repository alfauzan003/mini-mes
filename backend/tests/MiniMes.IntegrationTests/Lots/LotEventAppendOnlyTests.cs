using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Shared.Data;
using Npgsql;

namespace MiniMes.IntegrationTests.Lots;

[Collection("api")]
public class LotEventAppendOnlyTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task InsertEventAsync(MesDbContext db) =>
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO lot.lot_event (lot_id, type, user_id, occurred_at)
            VALUES ({Guid.NewGuid()}, 'Register', {Guid.NewGuid()}, now())
            """, Ct);

    [Fact]
    public async Task Updating_a_lot_event_is_rejected_by_database()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        await InsertEventAsync(db);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE lot.lot_event SET note = 'x'", Ct));

        var pg = Assert.IsType<PostgresException>(ex.GetBaseException());
        Assert.Contains("append-only", pg.Message);
    }

    [Fact]
    public async Task Deleting_a_lot_event_is_rejected_by_database()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        await InsertEventAsync(db);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM lot.lot_event", Ct));

        var pg = Assert.IsType<PostgresException>(ex.GetBaseException());
        Assert.Contains("append-only", pg.Message);
    }
}
