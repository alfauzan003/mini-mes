using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.WorkOrders.Data;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_order", "wo");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Number).HasMaxLength(32).IsRequired();
        builder.HasIndex(w => w.Number).IsUnique();
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(w => w.StatusBeforeHold).HasConversion<string>().HasMaxLength(16);
        builder.Property(w => w.Version).IsRowVersion();
        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.ProductId);
        builder.HasMany(w => w.Operations).WithOne().HasForeignKey(o => o.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Operations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
