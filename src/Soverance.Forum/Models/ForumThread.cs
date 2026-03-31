namespace Soverance.Forum.Models;

public class ForumThread
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastPostAt { get; set; }
    public ForumCategory Category { get; set; } = null!;
    public List<ForumPost> Posts { get; set; } = [];
}
