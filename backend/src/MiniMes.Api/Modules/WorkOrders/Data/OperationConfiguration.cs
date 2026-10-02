using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.WorkOrders.Data;

public class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("operation", "wo");
        builder.HasKey(o => o.Code);
        builder.Property(o => o.Code).HasConversion<string>().HasMaxLength(16).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(64).IsRequired();
        builder.Property(o => o.OutputLotType).HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.Uom).HasMaxLength(16).IsRequired();
    }
}
