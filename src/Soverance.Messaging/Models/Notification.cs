namespace Soverance.Messaging.Models;

public class Notification
{
    public long Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public string? Snippet { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
