using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Quality.Domain;

namespace MiniMes.Api.Modules.Quality.Data;

public class InspectionMeasurementConfiguration : IEntityTypeConfiguration<InspectionMeasurement>
{
    public void Configure(EntityTypeBuilder<InspectionMeasurement> builder)
    {
        builder.ToTable("inspection_measurement", "qc");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ItemName).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Unit).HasMaxLength(16).IsRequired();
        builder.Property(m => m.Lsl).HasColumnType("numeric(12,4)");
        builder.Property(m => m.Usl).HasColumnType("numeric(12,4)");
        builder.Property(m => m.Value).HasColumnType("numeric(12,4)");
        builder.Property(m => m.Judgment).HasConversion<string>().HasMaxLength(8);
        builder.HasOne<InspectionSpec>().WithMany().HasForeignKey(m => m.SpecId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.InspectionId);
        builder.HasIndex(m => m.SpecId);
    }
}
