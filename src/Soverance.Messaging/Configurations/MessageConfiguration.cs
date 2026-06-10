using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Messaging.Models;

namespace Soverance.Messaging.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Body).IsRequired().HasMaxLength(4000);
        builder.HasIndex(m => new { m.ConversationId, m.Id });
    }
}
