using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.WorkOrders.Data;

public class WorkOrderOperationConfiguration : IEntityTypeConfiguration<WorkOrderOperation>
{
    public void Configure(EntityTypeBuilder<WorkOrderOperation> builder)
    {
        builder.ToTable("work_order_operation", "wo");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Operation).HasConversion<string>().HasMaxLength(16);
        builder.HasOne<Operation>().WithMany().HasForeignKey(o => o.Operation).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.WorkOrderId, o.Operation }).IsUnique();
        builder.HasIndex(o => o.EquipmentId);
    }
}
