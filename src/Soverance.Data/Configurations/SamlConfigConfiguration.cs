using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Auth.Models;

namespace Soverance.Data.Configurations;

public class SamlConfigConfiguration : IEntityTypeConfiguration<SamlConfig>
{
    public void Configure(EntityTypeBuilder<SamlConfig> builder)
    {
        builder.HasKey(e => e.SamlConfigId);

        builder.Property(e => e.IdpEntityId).IsRequired().HasMaxLength(500);
        builder.Property(e => e.IdpSsoUrl).IsRequired().HasMaxLength(500);
        builder.Property(e => e.IdpSloUrl).HasMaxLength(500);
        builder.Property(e => e.IdpCertificate).IsRequired();
        builder.Property(e => e.SpEntityId).IsRequired().HasMaxLength(500);
    }
}
