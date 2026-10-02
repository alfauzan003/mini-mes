using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Shared.Data;

namespace MiniMes.Api.Modules.Lots.Features.Genealogy;

/// <summary>Backward walks from a lot to what it was made of; forward walks to what was made from it.</summary>
public enum GenealogyDirection
{
    Backward,
    Forward
}

/// <summary>The lots reachable from the root, root included, and the parent-to-child links between them.</summary>
public sealed record GenealogyGraph(
    string RootLotId, IReadOnlyList<LotDto> Nodes, IReadOnlyList<GenealogyEdge> Edges);

/// <summary>The parent lot was consumed to produce the child lot.</summary>
public sealed record GenealogyEdge(string ParentLotId, string ChildLotId);

public sealed class GenealogyQuery(MesDbContext db, LotQueries lots)
{
    /// <summary>Deepest chain followed from the root; the longest real path (RAW to pancake) is 3 links.</summary>
    private const int MaxDepth = 20;

    // The two walks differ only in which column is matched against the frontier and which one becomes the next
    // frontier. {0} is the root lot's id; the column names are fixed constants, never user input.
    private const string WalkTemplate = """
        WITH RECURSIVE walk (parent_lot_id, child_lot_id, depth) AS (
            SELECT g.parent_lot_id, g.child_lot_id, 1
            FROM lot.genealogy g
            WHERE g.{match} = {0}
            UNION
            SELECT g.parent_lot_id, g.child_lot_id, w.depth + 1
            FROM lot.genealogy g
            JOIN walk w ON g.{match} = w.{next}
            WHERE w.depth < {maxDepth}
        )
        SELECT DISTINCT p.lot_id AS parent_lot_id, c.lot_id AS child_lot_id
        FROM walk w
        JOIN lot.lot p ON p.id = w.parent_lot_id
        JOIN lot.lot c ON c.id = w.child_lot_id
        ORDER BY 1, 2
        """;

    private static readonly string BackwardSql = WalkTemplate
        .Replace("{match}", "child_lot_id").Replace("{next}", "parent_lot_id").Replace("{maxDepth}", MaxDepth.ToString());
    private static readonly string ForwardSql = WalkTemplate
        .Replace("{match}", "parent_lot_id").Replace("{next}", "child_lot_id").Replace("{maxDepth}", MaxDepth.ToString());

    /// <summary>Null when the lot does not exist.</summary>
    public async Task<GenealogyGraph?> GetAsync(string lotId, GenealogyDirection direction, CancellationToken ct)
    {
        var normalized = lotId.Trim().ToUpperInvariant();
        var root = await db.Set<Lot>().AsNoTracking()
            .Where(l => l.LotId == normalized)
            .Select(l => new { l.Id, l.LotId })
            .SingleOrDefaultAsync(ct);
        if (root is null)
        {
            return null;
        }

        var sql = direction == GenealogyDirection.Backward ? BackwardSql : ForwardSql;
        var edges = await db.Database.SqlQueryRaw<GenealogyEdge>(sql, root.Id).ToListAsync(ct);

        var lotIds = edges.SelectMany(e => new[] { e.ParentLotId, e.ChildLotId }).Append(root.LotId).Distinct().ToArray();
        var nodes = await lots.Project(db.Set<Lot>().AsNoTracking().Where(l => lotIds.Contains(l.LotId))).ToListAsync(ct);
        return new GenealogyGraph(root.LotId, nodes, edges);
    }
}
