using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Lots.Domain;

namespace MiniMes.Api.Modules.Lots.Data;

public class GenealogyLinkConfiguration : IEntityTypeConfiguration<GenealogyLink>
{
    public void Configure(EntityTypeBuilder<GenealogyLink> builder)
    {
        builder.ToTable("genealogy", "lot");
        builder.HasKey(g => new { g.ParentLotId, g.ChildLotId });
        builder.HasIndex(g => g.ChildLotId);
        builder.HasIndex(g => g.RunId);
    }
}
