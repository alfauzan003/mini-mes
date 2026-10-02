using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using MiniMes.Api.Shared.Time;

namespace MiniMes.IntegrationTests.Lots;

[Collection("api")]
public class LotIdGeneratorTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<string>> GenerateAsync(Func<LotIdGenerator, CancellationToken, Task<Result<string>>> next)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        var gen = scope.ServiceProvider.GetRequiredService<LotIdGenerator>();
        return await db.ExecuteInTransactionAsync(ct => next(gen, ct), Ct);
    }

    [Fact]
    public async Task Parallel_generation_yields_unique_sequential_ids()
    {
        var prefix = LotIdFormat.Prefix(LotType.Raw, Polarity.Cathode, PlantToday(), null);
        var before = await LastValueAsync(prefix);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            GenerateAsync((gen, ct) => gen.NextLotIdAsync(LotType.Raw, Polarity.Cathode, null, ct))));

        var ids = results.Select(r => r.Value).ToList();
        Assert.Equal(20, ids.Distinct().Count());
        Assert.All(ids, id => Assert.StartsWith(prefix + "-", id));
        var suffixes = ids.Select(id => int.Parse(id[(prefix.Length + 1)..])).Order();
        Assert.Equal(Enumerable.Range(before + 1, 20), suffixes);
    }

    [Fact]
    public async Task Work_order_numbers_increment()
    {
        var first = await GenerateAsync((gen, ct) => gen.NextWorkOrderNumberAsync(ct));
        var second = await GenerateAsync((gen, ct) => gen.NextWorkOrderNumberAsync(ct));

        Assert.StartsWith("WO-", first.Value);
        Assert.EndsWith("-001", first.Value);
        Assert.StartsWith("WO-", second.Value);
        Assert.EndsWith("-002", second.Value);
    }

    private DateOnly PlantToday()
    {
        using var scope = api.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PlantCalendar>()
            .DateOf(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow());
    }

    private async Task<int> LastValueAsync(string prefix)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        var values = await db.Database
            .SqlQuery<int>($"SELECT last_value AS \"Value\" FROM lot.id_sequence WHERE prefix = {prefix}")
            .ToListAsync(Ct);
        return values.SingleOrDefault();
    }
}
