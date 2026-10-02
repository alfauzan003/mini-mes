using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Shared.Data;

namespace MiniMes.Api.Modules.Execution;

/// <summary>
/// Serializes commands on one production run. The run row has no concurrency token, so two requests that each
/// read "no outputs yet" would both commit; taking the row lock first makes the second one wait and then see
/// the first one's committed outputs. Call it inside the command's transaction, before loading the run.
/// </summary>
public static class RunLock
{
    /// <returns>False when no run has the ID (nothing was locked).</returns>
    public static async Task<bool> AcquireAsync(this MesDbContext db, Guid runId, CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<int>($"SELECT 1 AS \"Value\" FROM exec.production_run WHERE id = {runId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.Count > 0;
    }
}
