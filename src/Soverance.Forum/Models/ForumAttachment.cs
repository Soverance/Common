namespace Soverance.Forum.Models;

public class ForumAttachment
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploadedBy { get; set; }
    public long? PostId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ForumPost? Post { get; set; }
}
