using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Quality.Data;

public class InspectionSpecConfiguration : IEntityTypeConfiguration<InspectionSpec>
{
    public void Configure(EntityTypeBuilder<InspectionSpec> builder)
    {
        builder.ToTable("inspection_spec", "qc");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.ItemName).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Unit).HasMaxLength(16).IsRequired();
        builder.Property(s => s.Lsl).HasColumnType("numeric(12,4)");
        builder.Property(s => s.Usl).HasColumnType("numeric(12,4)");
        builder.HasOne<Product>().WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Operation>().WithMany().HasForeignKey(s => s.Operation).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.ProductId, s.Operation, s.ItemName }).IsUnique();
    }
}
