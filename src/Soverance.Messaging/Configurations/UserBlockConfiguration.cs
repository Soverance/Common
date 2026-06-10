using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Messaging.Models;

namespace Soverance.Messaging.Configurations;

public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => new { b.BlockerUserId, b.BlockedUserId }).IsUnique();
        builder.HasIndex(b => b.BlockedUserId);
    }
}
