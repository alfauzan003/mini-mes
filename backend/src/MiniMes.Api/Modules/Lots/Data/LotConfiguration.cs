using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Lots.Domain;

namespace MiniMes.Api.Modules.Lots.Data;

public class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("lot", "lot");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.LotId).HasMaxLength(32).IsRequired();
        builder.HasIndex(l => l.LotId).IsUnique();
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Polarity).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Qty).HasColumnType("numeric(12,3)");
        builder.Property(l => l.Uom).HasMaxLength(16).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Quality).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.CurrentOperation).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.NextOperation).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Version).IsRowVersion();
        builder.HasIndex(l => new { l.Status, l.Type });
        builder.HasIndex(l => l.WorkOrderId);
    }
}
