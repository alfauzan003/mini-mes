using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Execution.Domain;

namespace MiniMes.Api.Modules.Execution.Data;

public class ProductionRunConfiguration : IEntityTypeConfiguration<ProductionRun>
{
    public void Configure(EntityTypeBuilder<ProductionRun> builder)
    {
        builder.ToTable("production_run", "exec");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.GoodQty).HasPrecision(12, 3);
        builder.Property(r => r.RejectQty).HasPrecision(12, 3);
        builder.HasIndex(r => r.WorkOrderOperationId);
        builder.HasIndex(r => r.EquipmentId);
        builder.HasMany(r => r.Inputs).WithOne().HasForeignKey(i => i.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Outputs).WithOne().HasForeignKey(o => o.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Inputs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(r => r.Outputs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(r => r.IsOpen);
        builder.Ignore(r => r.PrimaryLotId);
    }
}
