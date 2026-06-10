namespace Soverance.Messaging.Models;

public class Conversation
{
    public int Id { get; set; }
    public Guid UserAId { get; set; }   // canonical: UserAId.CompareTo(UserBId) < 0
    public Guid UserBId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastMessageAt { get; set; }
    public List<ConversationParticipant> Participants { get; set; } = [];
    public List<Message> Messages { get; set; } = [];
}
