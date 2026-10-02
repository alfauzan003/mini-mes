using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniMes.Api.Modules.Execution.Domain;

namespace MiniMes.Api.Modules.Execution.Data;

public class RunOutputConfiguration : IEntityTypeConfiguration<RunOutput>
{
    public void Configure(EntityTypeBuilder<RunOutput> builder)
    {
        builder.ToTable("run_output", "exec");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.GoodQty).HasPrecision(12, 3);
        builder.Property(o => o.RejectQty).HasPrecision(12, 3);
        builder.HasIndex(o => o.RunId);
        builder.HasIndex(o => o.LotId);
    }
}
