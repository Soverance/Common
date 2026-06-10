namespace Soverance.Messaging.Models;

public class UserBlock
{
    public long Id { get; set; }
    public Guid BlockerUserId { get; set; }
    public Guid BlockedUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
