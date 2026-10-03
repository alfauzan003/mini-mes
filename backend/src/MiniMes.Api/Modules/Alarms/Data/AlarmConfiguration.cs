using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Identity;
using AlarmCodeEntity = MiniMes.Api.Modules.Alarms.Domain.AlarmCode;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Alarms.Data;

public class AlarmConfiguration : IEntityTypeConfiguration<Alarm>
{
    public void Configure(EntityTypeBuilder<Alarm> builder)
    {
        builder.ToTable("alarm", "alarm");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.AlarmCode).HasMaxLength(32).IsRequired();
        builder.Property(a => a.Severity).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(a => a.IsActive);
        builder.Property(a => a.Version).IsRowVersion();
        builder.HasOne<EquipmentEntity>().WithMany().HasForeignKey(a => a.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AlarmCodeEntity>().WithMany().HasForeignKey(a => a.AlarmCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.AcknowledgedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.EquipmentId, a.RaisedAt });
        builder.HasIndex(a => new { a.EquipmentId, a.AlarmCode })
            .IsUnique()
            .HasFilter("cleared_at IS NULL");
    }
}
