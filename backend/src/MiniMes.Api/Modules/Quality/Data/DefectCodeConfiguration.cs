using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Quality.Data;

public class DefectCodeConfiguration : IEntityTypeConfiguration<DefectCode>
{
    public void Configure(EntityTypeBuilder<DefectCode> builder)
    {
        builder.ToTable("defect_code", "qc");
        builder.HasKey(d => d.Code);
        builder.Property(d => d.Code).HasMaxLength(32).ValueGeneratedNever();
        builder.Property(d => d.Description).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Operation).HasConversion<string>().HasMaxLength(16);
        builder.HasOne<Operation>().WithMany().HasForeignKey(d => d.Operation).OnDelete(DeleteBehavior.Restrict);
    }
}
