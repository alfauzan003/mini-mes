using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Alarms.Features.Queries;

public static class AlarmEndpoints
{
    public static void MapAlarmQueries(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/alarms", async (
            bool? active, string? equipment, string? severity, DateTimeOffset? from, DateTimeOffset? to,
            AlarmQueries queries, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParse<AlarmSeverity>(severity, out var parsedSeverity))
            {
                return EnumQuery.Invalid(nameof(severity), severity);
            }

            if (from is not null && to is not null && from > to)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(from)] = ["from must not be after to."]
                });
            }

            return TypedResults.Ok(await queries.ListAsync(
                new AlarmFilter(active, equipment, parsedSeverity, from, to), ct));
        }).RequireAuthorization();

    public static Error NotFound(Guid id) =>
        new(ErrorCodes.AlarmNotFound, $"Alarm {id} was not found.", ErrorKind.NotFound);
}
