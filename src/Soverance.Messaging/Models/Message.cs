namespace Soverance.Messaging.Models;

public class Message
{
    public long Id { get; set; }
    public int ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Conversation Conversation { get; set; } = null!;
}
