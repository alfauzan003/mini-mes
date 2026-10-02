using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Execution.Domain;

namespace MiniMes.Api.Modules.Execution.Data;

public class RunInputConfiguration : IEntityTypeConfiguration<RunInput>
{
    public void Configure(EntityTypeBuilder<RunInput> builder)
    {
        builder.ToTable("run_input", "exec");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.ConsumedQty).HasPrecision(12, 3);
        builder.HasIndex(i => i.RunId);
        builder.HasIndex(i => i.LotId);
    }
}
