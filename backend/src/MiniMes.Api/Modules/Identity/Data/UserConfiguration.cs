using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiniMes.Api.Modules.Identity.Data;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user", "identity");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(64).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(128).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
    }
}
