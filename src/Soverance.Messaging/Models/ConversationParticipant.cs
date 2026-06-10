namespace Soverance.Messaging.Models;

public class ConversationParticipant
{
    public int ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }
    public bool IsHidden { get; set; }
    public Conversation Conversation { get; set; } = null!;
}
