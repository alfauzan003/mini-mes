using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Carriers.Domain;

namespace MiniMes.Api.Modules.Carriers.Data;

public class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> builder)
    {
        builder.ToTable("carrier", "carrier");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Code).HasMaxLength(16).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.TypeCode).HasMaxLength(8).IsRequired();
        builder.HasOne(c => c.Type).WithMany().HasForeignKey(c => c.TypeCode).OnDelete(DeleteBehavior.Restrict);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.Version).IsRowVersion();
        builder.HasIndex(c => new { c.TypeCode, c.Status });
    }
}
