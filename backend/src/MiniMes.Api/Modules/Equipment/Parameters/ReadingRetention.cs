using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Shared.Data;

namespace MiniMes.Api.Modules.Equipment.Parameters;

public sealed class ReadingRetention(MesDbContext db, TimeProvider time, IOptions<ParametersOptions> options)
{
    /// <summary>Deletes stored readings older than the retention window; returns how many.</summary>
    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return await db.Set<ParameterReading>().Where(r => r.RecordedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
