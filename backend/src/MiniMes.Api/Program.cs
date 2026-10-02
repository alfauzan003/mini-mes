using MiniMes.Api.Shared.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.ConfigureMesJson());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/health", () => TypedResults.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
