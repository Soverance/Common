namespace Soverance.Forum.Models;

public class ForumPost
{
    public long Id { get; set; }
    public int ThreadId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ForumThread Thread { get; set; } = null!;
    public List<ForumVote> Votes { get; set; } = [];
    public List<ForumAttachment> Attachments { get; set; } = [];
}
