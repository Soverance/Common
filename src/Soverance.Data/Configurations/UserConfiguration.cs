using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Auth.Models;

namespace Soverance.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.ApiKey).IsUnique();
        builder.HasIndex(u => new { u.OAuthProvider, u.OAuthId }).IsUnique();

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Username).HasMaxLength(64).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(128);
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.ApiKey).HasMaxLength(128);
        builder.Property(u => u.ApiKeyCreatedAt);
        builder.Property(u => u.OAuthProvider).HasMaxLength(32);
        builder.Property(u => u.OAuthId).HasMaxLength(256);
        builder.Property(u => u.DefaultServer).HasMaxLength(64);
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(UserRole.Member);
    }
}
