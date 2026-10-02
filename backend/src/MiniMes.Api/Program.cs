using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
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

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/health", async (MesDbContext db, CancellationToken ct) =>
    TypedResults.Ok(new
    {
        status = "ok",
        database = await db.Database.CanConnectAsync(ct) ? "up" : "down"
    }));

app.MapIdentityEndpoints(app.Configuration.GetValue<bool>("Demo:EnableQuickLogin"));

await app.InitializeDatabaseAsync();

app.Run();

public partial class Program;
