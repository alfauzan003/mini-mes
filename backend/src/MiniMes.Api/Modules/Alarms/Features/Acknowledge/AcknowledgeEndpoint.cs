using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Alarms.Features.Queries;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Alarms.Features.Acknowledge;

/// <summary>Records who acknowledged an alarm and when; an alarm is acknowledged at most once.</summary>
public sealed class AcknowledgeHandler(MesDbContext db, AlarmQueries queries, ICurrentUser user, TimeProvider time)
{
    public async Task<Result<AlarmDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        var acknowledged = await db.ExecuteInTransactionAsync<Guid>(async token =>
        {
            var alarm = await db.Set<Alarm>().SingleOrDefaultAsync(a => a.Id == id, token);
            if (alarm is null)
            {
                return AlarmEndpoints.NotFound(id);
            }

            var result = alarm.Acknowledge(user.UserId, time.GetUtcNow());
            if (!result.IsSuccess)
            {
                return result.Error!;
            }

            return alarm.Id;
        }, ct);
        if (!acknowledged.IsSuccess)
        {
            return acknowledged.Error!;
        }

        return (await queries.GetAsync(acknowledged.Value, ct))!;
    }
}

public static class AcknowledgeEndpoint
{
    public static void MapAcknowledge(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/alarms/{id:guid}/acknowledge", async (Guid id, AcknowledgeHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Operate);
}
