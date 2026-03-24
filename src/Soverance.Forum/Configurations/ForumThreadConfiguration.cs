using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Forum.Models;

namespace Soverance.Forum.Configurations;

public class ForumThreadConfiguration : IEntityTypeConfiguration<ForumThread>
{
    public void Configure(EntityTypeBuilder<ForumThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.AuthorId).IsRequired();
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => new { t.CategoryId, t.Slug }).IsUnique();
        builder.Property(t => t.IsPinned).HasDefaultValue(false);
        builder.Property(t => t.IsLocked).HasDefaultValue(false);
        builder.HasIndex(t => t.LastPostAt);
        builder.HasMany(t => t.Posts).WithOne(p => p.Thread).HasForeignKey(p => p.ThreadId).OnDelete(DeleteBehavior.Cascade);
    }
}
