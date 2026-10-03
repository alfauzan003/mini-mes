using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MiniMes.Simulator;

public sealed class SimulatorWorker(
    IOptions<SimulatorOptions> options, TimeProvider time, ILoggerFactory loggerFactory,
    ILogger<SimulatorWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var rng = opts.RandomSeed is { } seed ? new Random(seed) : new Random();
        var connection = SimulatorConnection.Build(opts);
        var session = new SimulatorSession(
            connection, opts, time, rng, loggerFactory.CreateLogger<SimulatorSession>());

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (connection.State == HubConnectionState.Disconnected)
                        await connection.StartAsync(stoppingToken);
                    await session.LoadAsync(stoppingToken);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("API not reachable, retrying: {Message}", ex.Message);
                    await Task.Delay(RetryDelay, time, stoppingToken);
                }
            }

            logger.LogInformation("Connected to {Url}", opts.HubUrl);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(opts.TickSeconds), time);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await session.TickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Typically the connection is mid-reconnect; the next tick tries again.
                    logger.LogWarning("Tick failed: {Message}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
