using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Lots.Domain;

namespace MiniMes.Api.Modules.Lots.Data;

public class LotEventConfiguration : IEntityTypeConfiguration<LotEvent>
{
    public void Configure(EntityTypeBuilder<LotEvent> builder)
    {
        builder.ToTable("lot_event", "lot");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Qty).HasColumnType("numeric(12,3)");
        builder.Property(e => e.Note).HasMaxLength(512);
        builder.HasIndex(e => new { e.LotId, e.OccurredAt });
    }
}
