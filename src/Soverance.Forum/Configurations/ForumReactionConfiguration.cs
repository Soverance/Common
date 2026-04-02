using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Forum.Models;

namespace Soverance.Forum.Configurations;

public class ForumReactionConfiguration : IEntityTypeConfiguration<ForumReaction>
{
    public void Configure(EntityTypeBuilder<ForumReaction> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.ReactionType).IsRequired();
        builder.HasIndex(r => new { r.PostId, r.UserId, r.ReactionType }).IsUnique();
    }
}
