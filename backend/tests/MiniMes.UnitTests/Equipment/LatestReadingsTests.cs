using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Parameters;

namespace MiniMes.UnitTests.EquipmentModule;

public class LatestReadingsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private static LiveReadingDto Reading(string equipment, string parameter, decimal value) =>
        new(equipment, parameter, ParameterKind.Temperature, "°C", value, 20m, 30m, T0);

    [Fact]
    public void First_reading_is_persisted()
    {
        var latest = new LatestReadings();

        Assert.True(latest.TryMarkPersisted("MX01", "Slurry temp", T0, Interval));
    }

    [Fact]
    public void Reading_within_interval_is_not_persisted()
    {
        var latest = new LatestReadings();
        latest.TryMarkPersisted("MX01", "Slurry temp", T0, Interval);

        Assert.False(latest.TryMarkPersisted("MX01", "Slurry temp", T0.AddSeconds(2), Interval));
        Assert.True(latest.TryMarkPersisted("MX01", "Vacuum", T0.AddSeconds(2), Interval));
        Assert.True(latest.TryMarkPersisted("MX02", "Slurry temp", T0.AddSeconds(2), Interval));
    }

    [Fact]
    public void Reading_after_interval_is_persisted_again()
    {
        var latest = new LatestReadings();
        latest.TryMarkPersisted("MX01", "Slurry temp", T0, Interval);

        Assert.True(latest.TryMarkPersisted("MX01", "Slurry temp", T0.AddSeconds(10), Interval));
    }

    [Fact]
    public void Update_replaces_previous_value()
    {
        var latest = new LatestReadings();
        latest.Update(Reading("MX01", "Slurry temp", 24m));
        latest.Update(Reading("MX01", "Slurry temp", 26m));

        var all = latest.All();

        Assert.Equal(26m, Assert.Single(all).Value);
    }
}
