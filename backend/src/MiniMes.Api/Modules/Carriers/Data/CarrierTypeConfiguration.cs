using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Carriers.Domain;

namespace MiniMes.Api.Modules.Carriers.Data;

public class CarrierTypeConfiguration : IEntityTypeConfiguration<CarrierType>
{
    public void Configure(EntityTypeBuilder<CarrierType> builder)
    {
        builder.ToTable("carrier_type", "carrier");
        builder.HasKey(t => t.Code);
        builder.Property(t => t.Code).HasMaxLength(8).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(64).IsRequired();
        builder.Property(t => t.AllowedLotType).HasConversion<string>().HasMaxLength(16);
    }
}
