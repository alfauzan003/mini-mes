using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace MiniMes.Simulator;

/// <summary>One simulator's conversation with the machine hub: loads the equipment, ticks the models and reports.</summary>
public sealed class SimulatorSession
{
    private readonly HubConnection _connection;
    private readonly SimulatorOptions _options;
    private readonly TimeProvider _time;
    private readonly Random _rng;
    private readonly ILogger<SimulatorSession> _logger;

    // Handlers run on hub threads while ticks run on the worker's, and MachineModel is not thread-safe.
    private readonly object _gate = new();
    private Dictionary<string, MachineModel> _models = new();

    public SimulatorSession(
        HubConnection connection, SimulatorOptions options, TimeProvider time, Random rng,
        ILogger<SimulatorSession> logger)
    {
        _connection = connection;
        _options = options;
        _time = time;
        _rng = rng;
        _logger = logger;

        _connection.On<string, EquipmentStatus>("EquipmentStateChanged", (code, status) =>
        {
            lock (_gate)
            {
                if (_models.TryGetValue(code, out var model))
                    model.SetStatus(status);
            }
        });
        _connection.On<string>("InjectFault", code =>
        {
            lock (_gate)
            {
                if (!_models.TryGetValue(code, out var model))
                    return;
                try
                {
                    model.InjectFault(_time.GetUtcNow(), _rng);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Cannot inject a fault into {Code}", code);
                }
            }
        });
        _connection.Reconnected += async _ =>
        {
            try
            {
                await LoadAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reloading equipment after reconnect failed");
            }
        };
    }

    /// <summary>The current status of a machine, or null when the equipment is unknown.</summary>
    public EquipmentStatus? StatusOf(string code)
    {
        lock (_gate)
            return _models.TryGetValue(code, out var model) ? model.Status : null;
    }

    public async Task LoadAsync(CancellationToken ct)
    {
        var states = await _connection.InvokeAsync<List<MachineEquipmentState>>("GetEquipmentStates", ct);
        var models = states.ToDictionary(s => s.Code, s => new MachineModel(s, _options));
        lock (_gate)
            _models = models;
        _logger.LogInformation("Loaded {Count} machines", models.Count);
    }

    public async Task TickAsync(CancellationToken ct)
    {
        List<(string Code, MachineTick Tick)> ticks;
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            ticks = [.. _models.Values.Select(m => (m.Code, m.Tick(now, _rng)))];
        }

        foreach (var (code, tick) in ticks)
        {
            // Each call stands alone: the model already advanced, so a failed report must not skip a Clear.
            if (tick.Readings.Count > 0)
                await SendAsync(code, "ReportReadings", [code, tick.Readings], ct);
            // Clear before raise: a first tick can both drop an adopted alarm and raise an injected one.
            foreach (var alarm in tick.Clear)
                await SendAsync(code, "ClearAlarm", [code, alarm], ct);
            foreach (var alarm in tick.Raise)
                await SendAsync(code, "RaiseAlarm", [code, alarm], ct);
        }
    }

    private async Task SendAsync(string code, string method, object?[] args, CancellationToken ct)
    {
        try
        {
            await _connection.InvokeCoreAsync(method, args, ct);
        }
        catch (HubException ex)
        {
            _logger.LogWarning(ex, "Hub rejected {Method} for {Code}", method, code);
        }
    }
}
