namespace Soverance.Forum.Models;

public class ForumReaction
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public Guid UserId { get; set; }
    public ReactionType ReactionType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ForumPost Post { get; set; } = null!;
}
