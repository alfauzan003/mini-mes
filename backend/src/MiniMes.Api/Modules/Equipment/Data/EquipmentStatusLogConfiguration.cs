using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Equipment.Domain;

namespace MiniMes.Api.Modules.Equipment.Data;

public class EquipmentStatusLogConfiguration : IEntityTypeConfiguration<EquipmentStatusLog>
{
    public void Configure(EntityTypeBuilder<EquipmentStatusLog> builder)
    {
        builder.ToTable("equipment_status_log", "eqp");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();
        builder.Property(l => l.From).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.To).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Reason).HasMaxLength(200).IsRequired();
        builder.HasIndex(l => new { l.EquipmentId, l.ChangedAt });
    }
}
