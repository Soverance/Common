namespace Soverance.Forum.Models;

public class ForumVote
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ForumPost Post { get; set; } = null!;
}
