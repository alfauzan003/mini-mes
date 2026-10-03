namespace MiniMes.Simulator;

public sealed record MachineTick(
    IReadOnlyList<ReadingInput> Readings,
    IReadOnlyList<string> Raise,
    IReadOnlyList<string> Clear);

/// <summary>Deterministic physical model of one machine; all randomness and time come from the caller.</summary>
public sealed class MachineModel
{
    private readonly MachineEquipmentState _state;
    private readonly SimulatorOptions _options;
    private readonly Dictionary<string, decimal> _values = new();
    private readonly HashSet<string> _active = new();
    private readonly List<string> _adopted;
    // Parameters that have reached their limits since the machine started running; ramping up from rest is not a fault.
    private readonly HashSet<string> _retryClear = new();
    private readonly HashSet<string> _settled = new();

    private string? _driftParameter;
    private int _driftTicksLeft;

    private string? _faultCode;
    private bool _faultPending;
    private DateTimeOffset _faultClearAt;

    private bool _firstTick = true;

    public MachineModel(MachineEquipmentState state, SimulatorOptions options)
    {
        _state = state;
        _options = options;
        Status = state.Status;
        foreach (var p in state.Parameters)
            _values[p.Name] = InitialValue(p, state.Status);
        _adopted = state.ActiveAlarmCodes.ToList();
        foreach (var code in _adopted)
            _active.Add(code);
    }

    public string Code => _state.Code;
    public EquipmentStatus Status { get; private set; }
    public IReadOnlyDictionary<string, decimal> Values => _values;
    public IReadOnlySet<string> ActiveAlarms => _active;

    public void SetStatus(EquipmentStatus status) => Status = status;

    public void InjectFault(DateTimeOffset now, Random rng)
    {
        var critical = _state.AlarmCodes.FirstOrDefault(a => a.Severity == AlarmSeverity.Critical)
            ?? throw new InvalidOperationException($"{Code} has no critical alarm code.");
        _faultCode = critical.Code;
        _faultPending = true;
        _faultClearAt = now.AddSeconds(30 + rng.NextDouble() * 30);
    }

    /// <summary>Puts back an alarm the hub rejected clearing, so the next tick reports the clear again.</summary>
    public void RetryClear(string code) => _retryClear.Add(code);

    /// <summary>Forgets an alarm the hub rejected raising, so the next evaluation raises it again.</summary>
    public void ForgetRaised(string code)
    {
        _active.Remove(code);
        if (code == _faultCode)
            _faultPending = true;
    }

    public void ForceDrift(string parameter, int ticks)
    {
        _driftParameter = parameter;
        _driftTicksLeft = ticks;
    }

    public MachineTick Tick(DateTimeOffset now, Random rng)
    {
        var raise = new List<string>();
        var clear = new List<string>();
        var running = Status == EquipmentStatus.Running;

        foreach (var code in _retryClear)
            clear.Add(code);
        _retryClear.Clear();

        if (_firstTick)
        {
            _firstTick = false;
            foreach (var code in _adopted)
            {
                if (_active.Remove(code))
                    clear.Add(code);
            }
        }

        if (!running)
        {
            _driftParameter = null;
            _settled.Clear();
        }
        else if (_driftParameter is null && rng.NextDouble() < _options.DriftChancePerTick)
        {
            var candidates = _state.Parameters
                .Where(p => p.HighAlarmCode is not null || p.LowAlarmCode is not null)
                .ToList();
            if (candidates.Count > 0)
            {
                _driftParameter = candidates[rng.Next(candidates.Count)].Name;
                _driftTicksLeft = rng.Next(10, 21);
            }
        }

        var readings = new List<ReadingInput>();
        foreach (var p in _state.Parameters)
        {
            var range = (double)(p.High - p.Low);
            var target = Target(p, running);
            var noise = (rng.NextDouble() * 2 - 1) * 0.01 * range;
            var value = _values[p.Name];
            value += (target - value) * 0.3m + (decimal)noise;
            value = Math.Round(value, 2);
            // Speeds and pressures (rpm, m/min, kPa, ton, N) cannot be negative; a temperature in °C can.
            if (p.Kind != ParameterKind.Temperature)
                value = Math.Max(value, 0m);
            _values[p.Name] = value;
            readings.Add(new ReadingInput(p.Name, value));

            if (running)
                Evaluate(p, value, raise, clear);
        }

        if (!running)
        {
            foreach (var p in _state.Parameters)
            {
                foreach (var code in new[] { p.LowAlarmCode, p.HighAlarmCode })
                {
                    if (code is not null && code != _faultCode && _active.Remove(code))
                        clear.Add(code);
                }
            }
        }

        // A drift requested before its parameter settled waits for it, so the excursion is a real one.
        if (_driftParameter is not null && _settled.Contains(_driftParameter) && --_driftTicksLeft <= 0)
            _driftParameter = null;

        if (_faultCode is not null)
        {
            if (_faultPending)
            {
                _faultPending = false;
                if (_active.Add(_faultCode))
                    raise.Add(_faultCode);
            }
            else if (_active.Contains(_faultCode) && now >= _faultClearAt)
            {
                _active.Remove(_faultCode);
                clear.Add(_faultCode);
            }
        }

        return new MachineTick(readings, raise, clear);
    }

    private decimal InitialValue(MachineParameterDto p, EquipmentStatus status)
    {
        if (status == EquipmentStatus.Running)
            return p.Setpoint;
        return p.Kind == ParameterKind.Temperature ? (decimal)_options.AmbientTemperature : 0m;
    }

    private decimal Target(MachineParameterDto p, bool running)
    {
        if (!running)
            return p.Kind == ParameterKind.Temperature ? (decimal)_options.AmbientTemperature : 0m;

        var target = p.Setpoint;
        if (_driftParameter == p.Name && _settled.Contains(p.Name))
        {
            var offset = (p.High - p.Low) * 0.6m;
            target += p.HighAlarmCode is not null ? offset : p.LowAlarmCode is not null ? -offset : 0m;
        }
        return target;
    }

    private void Evaluate(MachineParameterDto p, decimal value, List<string> raise, List<string> clear)
    {
        if (!_settled.Contains(p.Name))
        {
            if (value < p.Low || value > p.High)
                return;
            _settled.Add(p.Name);
        }

        if (value > p.High && p.HighAlarmCode is not null && _active.Add(p.HighAlarmCode))
            raise.Add(p.HighAlarmCode);
        else if (value < p.Low && p.LowAlarmCode is not null && _active.Add(p.LowAlarmCode))
            raise.Add(p.LowAlarmCode);
        else if (value >= p.Low && value <= p.High)
        {
            foreach (var code in new[] { p.LowAlarmCode, p.HighAlarmCode })
            {
                if (code is not null && _active.Remove(code))
                    clear.Add(code);
            }
        }
    }
}
