using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Lots.Domain;

namespace MiniMes.Api.Modules.Lots.Data;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("material", "lot");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(m => m.Code).IsUnique();
        builder.Property(m => m.Name).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Polarity).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Uom).HasMaxLength(16).IsRequired();
    }
}
