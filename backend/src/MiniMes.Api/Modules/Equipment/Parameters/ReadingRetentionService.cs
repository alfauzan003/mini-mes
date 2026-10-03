using Microsoft.Extensions.Options;

namespace MiniMes.Api.Modules.Equipment.Parameters;

/// <summary>Purges old readings once at start and then every hour.</summary>
public sealed class ReadingRetentionService(
    IServiceScopeFactory scopes, IOptions<ParametersOptions> options, ILogger<ReadingRetentionService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (options.Value.RetentionInitialDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.RetentionInitialDelaySeconds), stoppingToken);
            }

            await PurgeSafelyAsync(stoppingToken);

            using var timer = new PeriodicTimer(Period);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PurgeSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PurgeSafelyAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<ReadingRetention>().PurgeAsync(ct);
            logger.LogInformation("Reading retention purged {Count} rows.", deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Reading retention purge failed.");
        }
    }
}
