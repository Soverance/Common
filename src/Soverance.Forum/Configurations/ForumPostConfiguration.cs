using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Forum.Models;

namespace Soverance.Forum.Configurations;

public class ForumPostConfiguration : IEntityTypeConfiguration<ForumPost>
{
    public void Configure(EntityTypeBuilder<ForumPost> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.AuthorId).IsRequired();
        builder.Property(p => p.Body).IsRequired();
        builder.Property(p => p.IsEdited).HasDefaultValue(false);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.HasIndex(p => new { p.ThreadId, p.CreatedAt });
        builder.HasMany(p => p.Votes).WithOne(v => v.Post).HasForeignKey(v => v.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}
