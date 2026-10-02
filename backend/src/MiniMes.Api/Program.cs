using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers;
using MiniMes.Api.Modules.Equipment;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots;
using MiniMes.Api.Modules.WorkOrders;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Data.Seed;
using MiniMes.Api.Shared.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.ConfigureMesJson());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Resolved lazily so test hosts can override ConnectionStrings:Mes after Program starts.
builder.Services.AddDbContext<MesDbContext>((sp, options) =>
    options
        .UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Mes"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddIdentityModule();
builder.Services.AddLotsModule();
builder.Services.AddWorkOrdersModule();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapGet("/api/health", async (MesDbContext db, CancellationToken ct) =>
    TypedResults.Ok(new
    {
        status = "ok",
        database = await db.Database.CanConnectAsync(ct) ? "up" : "down"
    })).AllowAnonymous();

app.MapIdentityEndpoints(app.Configuration.GetValue<bool>("Demo:EnableQuickLogin"));
app.MapWorkOrdersEndpoints();
app.MapLotsEndpoints();
app.MapEquipmentEndpoints();
app.MapCarriersEndpoints();

// The authorization fallback policy also guards requests that match no route, which would answer
// 401 instead of 404. An anonymous catch-all keeps unknown and unmapped paths at 404.
app.MapFallback(() => Results.NotFound()).AllowAnonymous();

await app.InitializeDatabaseAsync();

app.Run();

public partial class Program;
