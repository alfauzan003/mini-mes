using Microsoft.Extensions.Options;

namespace MiniMes.Api.Shared.Time;

/// <summary>Maps instants to the plant's local calendar date, which dates lot and work order IDs.</summary>
public sealed class PlantCalendar(IOptions<PlantOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly DateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _zone).DateTime);
}
