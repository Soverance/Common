using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Forum.Models;

namespace Soverance.Forum.Configurations;

public class ForumAttachmentConfiguration : IEntityTypeConfiguration<ForumAttachment>
{
    public void Configure(EntityTypeBuilder<ForumAttachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(256);
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(512);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(64);
        builder.Property(a => a.FileSize).IsRequired();
        builder.Property(a => a.UploadedBy).IsRequired();
        builder.HasIndex(a => a.PostId);
        builder.HasIndex(a => a.UploadedBy);
        builder.HasOne(a => a.Post)
            .WithMany(p => p.Attachments)
            .HasForeignKey(a => a.PostId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
