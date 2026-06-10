using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Messaging.Models;

namespace Soverance.Messaging.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.RecipientUserId).IsRequired();
        builder.Property(n => n.Type).IsRequired().HasMaxLength(64);
        builder.Property(n => n.TargetUrl).IsRequired().HasMaxLength(512);
        builder.Property(n => n.Snippet).HasMaxLength(280);
        builder.Property(n => n.IsRead).HasDefaultValue(false);

        // Serves ListAsync (filter by recipient, order/cursor by Id desc).
        builder.HasIndex(n => new { n.RecipientUserId, n.Id });
        // Serves GetUnreadCountAsync (filter by recipient + unread).
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead });
    }
}
