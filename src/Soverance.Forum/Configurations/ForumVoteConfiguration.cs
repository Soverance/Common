using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Forum.Models;

namespace Soverance.Forum.Configurations;

public class ForumVoteConfiguration : IEntityTypeConfiguration<ForumVote>
{
    public void Configure(EntityTypeBuilder<ForumVote> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.UserId).IsRequired();
        builder.HasIndex(v => new { v.PostId, v.UserId }).IsUnique();
    }
}
