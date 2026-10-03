namespace MiniMes.Simulator.Tests;

public class MachineModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private static MachineEquipmentState Coat(EquipmentStatus status = EquipmentStatus.Running, params string[] active) => new(
        "COAT-01", OperationCode.Coat, status,
        [
            new("Dryer temp", ParameterKind.Temperature, "C", 130, 120, 140, null, "CT-TEMP-HIGH"),
            new("Line speed", ParameterKind.Speed, "m/min", 40, 30, 50, null, null),
            new("Slot-die pressure", ParameterKind.Pressure, "kPa", 150, 130, 170, "CT-DIE-PRESS-LOW", null)
        ],
        [
            new("CT-TEMP-HIGH", AlarmSeverity.Major),
            new("CT-DIE-PRESS-LOW", AlarmSeverity.Warning),
            new("CT-WEB-BREAK", AlarmSeverity.Critical)
        ],
        active);

    private static SimulatorOptions Options(double drift = 0) => new()
    {
        HubUrl = "http://localhost/hubs/machine",
        ApiKey = "k",
        DriftChancePerTick = drift
    };

    [Fact]
    public void Running_values_stay_within_limits_without_drift()
    {
        var model = new MachineModel(Coat(), Options());
        var rng = new Random(42);
        var now = T0;
        for (var i = 0; i < 200; i++)
        {
            var tick = model.Tick(now, rng);
            now = now.AddSeconds(2);
            Assert.Equal(3, tick.Readings.Count);
            Assert.Empty(tick.Raise);
            Assert.Empty(tick.Clear);
            Assert.InRange(model.Values["Dryer temp"], 120m, 140m);
            Assert.InRange(model.Values["Line speed"], 30m, 50m);
            Assert.InRange(model.Values["Slot-die pressure"], 130m, 170m);
        }
    }

    [Fact]
    public void Idle_speed_goes_to_zero_and_temperature_decays_to_ambient()
    {
        var model = new MachineModel(Coat(), Options());
        var rng = new Random(42);
        var now = T0;
        for (var i = 0; i < 30; i++) { model.Tick(now, rng); now = now.AddSeconds(2); }
        model.SetStatus(EquipmentStatus.Idle);
        for (var i = 0; i < 40; i++) { model.Tick(now, rng); now = now.AddSeconds(2); }

        Assert.True(model.Values["Line speed"] < 0.5m);
        Assert.InRange(model.Values["Dryer temp"], 24m, 26m);
    }

    [Fact]
    public void Forced_drift_raises_high_alarm_once_and_clears_after_return()
    {
        var model = new MachineModel(Coat(), Options());
        var rng = new Random(42);
        var now = T0;
        model.ForceDrift("Dryer temp", 15);

        var raises = new List<string>();
        var clearTick = -1;
        for (var i = 0; i < 30; i++)
        {
            var tick = model.Tick(now, rng);
            now = now.AddSeconds(2);
            raises.AddRange(tick.Raise);
            if (tick.Clear.Contains("CT-TEMP-HIGH")) { clearTick = i; break; }
        }

        Assert.Equal(["CT-TEMP-HIGH"], raises);
        Assert.InRange(clearTick, 15, 29);
        Assert.Empty(model.ActiveAlarms);
    }

    [Theory]
    [InlineData(EquipmentStatus.Idle)]
    [InlineData(EquipmentStatus.Down)]
    [InlineData(EquipmentStatus.Maintenance)]
    public void No_alarms_while_not_running(EquipmentStatus status)
    {
        var model = new MachineModel(Coat(status), Options(drift: 1));
        var rng = new Random(42);
        var now = T0;
        for (var i = 0; i < 100; i++)
        {
            var tick = model.Tick(now, rng);
            now = now.AddSeconds(2);
            Assert.Empty(tick.Raise);
            Assert.Empty(tick.Clear);
        }
        Assert.Empty(model.ActiveAlarms);
    }

    [Fact]
    public void Leaving_running_clears_parameter_alarms()
    {
        var model = new MachineModel(Coat(), Options());
        var rng = new Random(42);
        var now = T0;
        model.ForceDrift("Dryer temp", 50);
        for (var i = 0; i < 20; i++) { model.Tick(now, rng); now = now.AddSeconds(2); }
        Assert.Contains("CT-TEMP-HIGH", model.ActiveAlarms);

        model.SetStatus(EquipmentStatus.Idle);
        var tick = model.Tick(now, rng);

        Assert.Equal(["CT-TEMP-HIGH"], tick.Clear);
        Assert.Empty(tick.Raise);
        Assert.Empty(model.ActiveAlarms);
    }

    [Fact]
    public void Injected_fault_raises_critical_and_clears_after_duration()
    {
        var model = new MachineModel(Coat(), Options());
        var rng = new Random(42);
        var now = T0;
        model.InjectFault(now, rng);

        var first = model.Tick(now, rng);
        Assert.Equal(["CT-WEB-BREAK"], first.Raise);

        DateTimeOffset? clearedAt = null;
        for (var i = 0; i < 40 && clearedAt is null; i++)
        {
            now = now.AddSeconds(2);
            var tick = model.Tick(now, rng);
            Assert.Empty(tick.Raise);
            if (tick.Clear.Contains("CT-WEB-BREAK")) clearedAt = now;
        }

        Assert.NotNull(clearedAt);
        var elapsed = (clearedAt!.Value - T0).TotalSeconds;
        Assert.InRange(elapsed, 30, 62);
        Assert.DoesNotContain("CT-WEB-BREAK", model.ActiveAlarms);
    }

    [Fact]
    public void Server_reported_active_alarms_are_cleared_on_first_tick()
    {
        var model = new MachineModel(Coat(EquipmentStatus.Running, "CT-WEB-BREAK", "CT-TEMP-HIGH"), Options());
        var rng = new Random(42);
        Assert.Contains("CT-WEB-BREAK", model.ActiveAlarms);

        var first = model.Tick(T0, rng);
        var second = model.Tick(T0.AddSeconds(2), rng);

        Assert.Equivalent(new[] { "CT-WEB-BREAK", "CT-TEMP-HIGH" }, first.Clear);
        Assert.Empty(first.Raise);
        Assert.Empty(second.Clear);
        Assert.Empty(model.ActiveAlarms);
    }
}
