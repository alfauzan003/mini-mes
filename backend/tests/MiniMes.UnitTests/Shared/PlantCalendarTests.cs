using Microsoft.Extensions.Options;
using MiniMes.Api.Shared.Time;

namespace MiniMes.UnitTests.Shared;

public class PlantCalendarTests
{
    private static PlantCalendar Jakarta() => new(Options.Create(new PlantOptions { TimeZone = "Asia/Jakarta" }));

    [Fact]
    public void Evening_utc_is_next_day_in_jakarta() =>
        Assert.Equal(new DateOnly(2026, 10, 3), Jakarta().DateOf(DateTimeOffset.Parse("2026-10-02T18:30:00Z")));

    [Fact]
    public void Morning_utc_is_same_day_in_jakarta() =>
        Assert.Equal(new DateOnly(2026, 10, 2), Jakarta().DateOf(DateTimeOffset.Parse("2026-10-02T06:00:00Z")));
}
