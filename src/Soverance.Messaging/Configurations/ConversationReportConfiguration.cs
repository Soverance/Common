using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Messaging.Models;

namespace Soverance.Messaging.Configurations;

public class ConversationReportConfiguration : IEntityTypeConfiguration<ConversationReport>
{
    public void Configure(EntityTypeBuilder<ConversationReport> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(16);
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.HasIndex(r => r.Status);
    }
}
