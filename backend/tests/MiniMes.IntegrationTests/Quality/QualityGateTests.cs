using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;

namespace MiniMes.IntegrationTests.Quality;

[Collection("api")]
public class QualityGateTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_specs_flow_still_finishes_pancakes()
    {
        var driver = new ProductionDriver(api);

        var flow = await driver.RunFullFlowAsync(8);

        var wo = await driver.WorkOrderAsync(flow.WorkOrder);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public async Task With_specs_uninspected_slurry_is_blocked_from_coat()
    {
        await api.SeedInspectionSpecsAsync();
        var driver = new ProductionDriver(api);
        var wo = await driver.CreateReleasedWorkOrderAsync();
        var raws = await driver.MaterialLotsAsync("NCM811", "PVDF", "SUPER-P", "NMP");
        var mix = await driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);
        var slurry = (await driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m))).Outputs.Single().LotId!;
        await driver.TrackOutOkAsync(mix.Id);
        var foil = (await driver.MaterialLotsAsync("AL-FOIL")).Single();

        var response = await driver.TrackInAsync("CT01", wo, OperationCode.Coat, foil, slurry);

        await ProductionDriver.AssertErrorAsync(response, 422, "LOT_QUALITY_PENDING");
    }

    [Fact]
    public async Task Pancakes_wait_for_inspection_when_slit_has_specs()
    {
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
            var product = await db.Set<Product>().SingleAsync(p => p.Code == "CATH-NCM811", Ct);
            db.Set<InspectionSpec>().Add(new InspectionSpec(product.Id, OperationCode.Slit, "Width", "mm", 99.8m, 100.2m, 1));
            await db.SaveChangesAsync(Ct);
        }

        var driver = new ProductionDriver(api);
        var flow = await driver.RunFullFlowAsync(8);

        var wo = await driver.WorkOrderAsync(flow.WorkOrder);
        Assert.Equal(0, wo.GoodCount);
        foreach (var pancake in flow.Pancakes)
        {
            var lot = await driver.LotAsync(pancake);
            Assert.Equal(LotStatus.Wait, lot.Status);
            Assert.Equal(QualityStatus.None, lot.Quality);
        }
    }
}
