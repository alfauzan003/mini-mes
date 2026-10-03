using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Equipment.Data;

public class ParameterDefinitionConfiguration : IEntityTypeConfiguration<ParameterDefinition>
{
    public void Configure(EntityTypeBuilder<ParameterDefinition> builder)
    {
        builder.ToTable("parameter_definition", "eqp");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.Name).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.Unit).HasMaxLength(16).IsRequired();
        builder.Property(p => p.Setpoint).HasPrecision(12, 3);
        builder.Property(p => p.Low).HasPrecision(12, 3);
        builder.Property(p => p.High).HasPrecision(12, 3);
        builder.Property(p => p.LowAlarmCode).HasMaxLength(32);
        builder.Property(p => p.HighAlarmCode).HasMaxLength(32);
        builder.HasIndex(p => new { p.Operation, p.Name }).IsUnique();
        builder.HasOne<Operation>().WithMany().HasForeignKey(p => p.Operation).OnDelete(DeleteBehavior.Restrict);
    }
}
