using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Equipment.Domain;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Data;

public class ParameterReadingConfiguration : IEntityTypeConfiguration<ParameterReading>
{
    public void Configure(EntityTypeBuilder<ParameterReading> builder)
    {
        builder.ToTable("parameter_reading", "eqp");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.Parameter).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Value).HasPrecision(12, 3);
        builder.HasIndex(r => new { r.EquipmentId, r.Parameter, r.RecordedAt });
        builder.HasIndex(r => r.RecordedAt);
        builder.HasOne<EquipmentEntity>().WithMany().HasForeignKey(r => r.EquipmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
