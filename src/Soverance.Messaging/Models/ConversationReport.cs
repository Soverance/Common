namespace Soverance.Messaging.Models;

public class ConversationReport
{
    public long Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public int ConversationId { get; set; }
    public long? MessageId { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = ReportStatus.Open;
    public DateTimeOffset CreatedAt { get; set; }
}
