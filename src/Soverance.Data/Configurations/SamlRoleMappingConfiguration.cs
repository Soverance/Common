using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Auth.Models;

namespace Soverance.Data.Configurations;

public class SamlRoleMappingConfiguration : IEntityTypeConfiguration<SamlRoleMapping>
{
    public void Configure(EntityTypeBuilder<SamlRoleMapping> builder)
    {
        builder.HasKey(e => e.SamlRoleMappingId);

        builder.Property(e => e.IdpGroupId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.RoleName).IsRequired().HasMaxLength(100);

        builder.HasOne(e => e.SamlConfig)
            .WithMany(e => e.RoleMappings)
            .HasForeignKey(e => e.SamlConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
