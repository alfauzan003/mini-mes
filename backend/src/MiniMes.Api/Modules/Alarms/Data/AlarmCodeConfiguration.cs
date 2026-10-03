using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Alarms.Data;

public class AlarmCodeConfiguration : IEntityTypeConfiguration<AlarmCode>
{
    public void Configure(EntityTypeBuilder<AlarmCode> builder)
    {
        builder.ToTable("alarm_code", "alarm");
        builder.HasKey(a => a.Code);
        builder.Property(a => a.Code).HasMaxLength(32).ValueGeneratedNever();
        builder.Property(a => a.Message).HasMaxLength(128).IsRequired();
        builder.Property(a => a.Severity).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Operation).HasConversion<string>().HasMaxLength(16);
        builder.HasOne<Operation>().WithMany().HasForeignKey(a => a.Operation).OnDelete(DeleteBehavior.Restrict);
    }
}
