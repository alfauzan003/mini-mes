using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.WorkOrders.Data;

public class ProductRouteStepConfiguration : IEntityTypeConfiguration<ProductRouteStep>
{
    public void Configure(EntityTypeBuilder<ProductRouteStep> builder)
    {
        builder.ToTable("product_route", "wo");
        builder.HasKey(s => new { s.ProductId, s.Operation });
        builder.Property(s => s.Operation).HasConversion<string>().HasMaxLength(16);
        builder.HasOne<Operation>().WithMany().HasForeignKey(s => s.Operation).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.ProductId, s.Seq }).IsUnique();
    }
}
