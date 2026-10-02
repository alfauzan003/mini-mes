using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Lots.LotIds;

namespace MiniMes.Api.Modules.Lots.Data;

public class IdSequenceConfiguration : IEntityTypeConfiguration<IdSequence>
{
    public void Configure(EntityTypeBuilder<IdSequence> builder)
    {
        builder.ToTable("id_sequence", "lot");
        builder.HasKey(s => s.Prefix);
        builder.Property(s => s.Prefix).HasMaxLength(64);
        builder.Property(s => s.LastValue).IsRequired();
    }
}
