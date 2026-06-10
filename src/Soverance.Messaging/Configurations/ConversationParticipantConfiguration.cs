using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Messaging.Models;

namespace Soverance.Messaging.Configurations;

public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.HasKey(p => new { p.ConversationId, p.UserId });
        builder.Property(p => p.IsHidden).HasDefaultValue(false);
        builder.HasIndex(p => new { p.UserId, p.IsHidden });
    }
}
