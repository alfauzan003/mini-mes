using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniMes.Simulator;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<SimulatorWorker>();
builder.Build().Run();
