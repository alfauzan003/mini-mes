using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Quality.Data;

public class InspectionConfiguration : IEntityTypeConfiguration<Inspection>
{
    public void Configure(EntityTypeBuilder<Inspection> builder)
    {
        builder.ToTable("inspection", "qc");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.Result).HasConversion<string>().HasMaxLength(8);
        builder.Property(i => i.DefectCode).HasMaxLength(32);
        builder.Property(i => i.Reason).HasMaxLength(512);
        builder.Property(i => i.RejectQty).HasColumnType("numeric(12,3)");
        builder.Property(i => i.Disposition).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.DispositionReason).HasMaxLength(512);
        builder.HasOne<Lot>().WithMany().HasForeignKey(i => i.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DefectCode>().WithMany().HasForeignKey(i => i.DefectCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Operation>().WithMany().HasForeignKey(i => i.Operation).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Measurements).WithOne().HasForeignKey(m => m.InspectionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Measurements).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(i => new { i.LotId, i.InspectedAt });
    }
}
