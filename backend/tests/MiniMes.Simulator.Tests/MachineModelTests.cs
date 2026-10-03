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

    private static MachineEquipmentState Mix(EquipmentStatus status) => new(
        "MX01", OperationCode.Mix, status,
        [
            new("Slurry temp", ParameterKind.Temperature, "C", 25, 20, 30, null, "MX-TEMP-HIGH"),
            new("Agitator speed", ParameterKind.Speed, "rpm", 1500, 1200, 1800, null, null),
            new("Vacuum", ParameterKind.Pressure, "kPa", 85, 75, 95, "MX-VAC-LOW", null)
        ],
        [
            new("MX-TEMP-HIGH", AlarmSeverity.Major),
            new("MX-VAC-LOW", AlarmSeverity.Warning),
            new("MX-AGITATOR-FAULT", AlarmSeverity.Critical)
        ],
        []);

    private static MachineEquipmentState Slit(EquipmentStatus status) => new(
        "SL01", OperationCode.Slit, status,
        [
            new("Motor temp", ParameterKind.Temperature, "C", 45, 30, 60, null, "SL-TEMP-HIGH"),
            new("Line speed", ParameterKind.Speed, "m/min", 80, 70, 90, null, null),
            new("Web tension", ParameterKind.Pressure, "N", 120, 100, 140, "SL-TENSION-LOW", null)
        ],
        [
            new("SL-TEMP-HIGH", AlarmSeverity.Major),
            new("SL-TENSION-LOW", AlarmSeverity.Warning),
            new("SL-BLADE-FAULT", AlarmSeverity.Critical)
        ],
        []);

    private static MachineEquipmentState Cal(EquipmentStatus status) => new(
        "CP01", OperationCode.Cal, status,
        [
            new("Roll temp", ParameterKind.Temperature, "C", 90, 80, 100, null, "CP-TEMP-HIGH"),
            new("Line speed", ParameterKind.Speed, "m/min", 30, 25, 35, null, null),
            new("Nip pressure", ParameterKind.Pressure, "ton", 300, 270, 330, null, "CP-NIP-PRESS-HIGH")
        ],
        [
            new("CP-TEMP-HIGH", AlarmSeverity.Major),
            new("CP-NIP-PRESS-HIGH", AlarmSeverity.Major),
            new("CP-ROLL-FAULT", AlarmSeverity.Critical)
        ],
        []);

    public static IEnumerable<object[]> RestCases()
    {
        foreach (var status in new[] { EquipmentStatus.Idle, EquipmentStatus.Down, EquipmentStatus.Maintenance })
        {
            yield return [Coat(status)];
            yield return [Mix(status)];
            yield return [Cal(status)];
            yield return [Slit(status)];
        }
    }

    [Theory]
    [MemberData(nameof(RestCases))]
    public void A_machine_at_rest_never_reports_a_negative_reading(MachineEquipmentState state)
    {
        var model = new MachineModel(state, Options());
        var rng = new Random(7);
        var now = T0;
        for (var i = 0; i < 2000; i++)
        {
            var tick = model.Tick(now, rng);
            now = now.AddSeconds(2);
            foreach (var reading in tick.Readings)
                Assert.True(reading.Value >= 0m, $"{reading.Parameter} = {reading.Value} at tick {i}");
        }
    }

    [Fact]
    public void A_machine_stopping_after_a_run_never_reports_a_negative_reading()
    {
        var model = new MachineModel(Mix(EquipmentStatus.Running), Options());
        var rng = new Random(7);
        var now = T0;
        for (var i = 0; i < 30; i++) { model.Tick(now, rng); now = now.AddSeconds(2); }

        model.SetStatus(EquipmentStatus.Idle);
        for (var i = 0; i < 2000; i++)
        {
            var tick = model.Tick(now, rng);
            now = now.AddSeconds(2);
            foreach (var reading in tick.Readings)
                Assert.True(reading.Value >= 0m, $"{reading.Parameter} = {reading.Value} at tick {i}");
        }
    }

    public static IEnumerable<object[]> RampCases()
    {
        foreach (var from in new[] { EquipmentStatus.Idle, EquipmentStatus.Down })
        {
            yield return [Coat(from)];
            yield return [Mix(from)];
            yield return [Slit(from)];
        }
    }

    [Theory]
    [MemberData(nameof(RampCases))]
    public void Starting_a_run_from_rest_raises_no_alarms_during_the_ramp(MachineEquipmentState state)
    {
        var model = new MachineModel(state, Options());
        var rng = new Random(42);
        var now = T0;
        for (var i = 0; i < 20; i++) { model.Tick(now, rng); now = now.AddSeconds(2); }

        model.SetStatus(EquipmentStatus.Running);
        for (var i = 0; i < 60; i++)
        {
            var tick = model.Tick(now, rng);
            now = now.AddSeconds(2);
            Assert.Empty(tick.Raise);
            Assert.Empty(tick.Clear);
        }

        Assert.Empty(model.ActiveAlarms);
        foreach (var p in state.Parameters)
            Assert.InRange(model.Values[p.Name], p.Low, p.High);
    }

    [Fact]
    public void A_drift_after_settling_still_alarms_on_a_pressure_parameter()
    {
        var model = new MachineModel(Mix(EquipmentStatus.Idle), Options());
        var rng = new Random(42);
        var now = T0;
        model.SetStatus(EquipmentStatus.Running);
        for (var i = 0; i < 40; i++) { model.Tick(now, rng); now = now.AddSeconds(2); }

        model.ForceDrift("Vacuum", 15);
        var raises = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            raises.AddRange(model.Tick(now, rng).Raise);
            now = now.AddSeconds(2);
        }

        Assert.Equal(["MX-VAC-LOW"], raises);
    }

    [Fact]
    public void A_drift_forced_before_settling_waits_for_the_parameter_then_alarms()
    {
        var model = new MachineModel(Slit(EquipmentStatus.Idle), Options());
        var rng = new Random(42);
        var now = T0;
        model.SetStatus(EquipmentStatus.Running);
        model.ForceDrift("Web tension", 15);

        var raises = new List<string>();
        for (var i = 0; i < 60; i++)
        {
            raises.AddRange(model.Tick(now, rng).Raise);
            now = now.AddSeconds(2);
        }

        Assert.Equal(["SL-TENSION-LOW"], raises);
    }

    [Fact]
    public void A_rejected_clear_is_reported_again_on_the_next_tick()
    {
        var model = new MachineModel(Coat(EquipmentStatus.Running, "CT-WEB-BREAK"), Options());
        var rng = new Random(42);
        var first = model.Tick(T0, rng);
        Assert.Equal(["CT-WEB-BREAK"], first.Clear);

        model.RetryClear("CT-WEB-BREAK");

        Assert.Equal(["CT-WEB-BREAK"], model.Tick(T0.AddSeconds(2), rng).Clear);
        Assert.Empty(model.Tick(T0.AddSeconds(4), rng).Clear);
    }

    [Fact]
    public void A_rejected_raise_is_attempted_again_while_still_out_of_limits()
    {
        var model = new MachineModel(Coat(), Options());
        var rng = new Random(42);
        var now = T0;
        model.ForceDrift("Dryer temp", 50);
        var raiseTick = -1;
        for (var i = 0; i < 30 && raiseTick < 0; i++)
        {
            if (model.Tick(now, rng).Raise.Contains("CT-TEMP-HIGH")) raiseTick = i;
            now = now.AddSeconds(2);
        }
        Assert.True(raiseTick >= 0);

        model.ForgetRaised("CT-TEMP-HIGH");

        Assert.Equal(["CT-TEMP-HIGH"], model.Tick(now, rng).Raise);
    }
}
