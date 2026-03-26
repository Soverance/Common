namespace Soverance.Forum.DTOs;

// Request DTOs
public record CreateCategoryRequest(string Name, string Description, int DisplayOrder = 0);
public record UpdateCategoryRequest(string Name, string Description, int DisplayOrder);
public record CreateThreadRequest(string Title, string Body);
public record CreatePostRequest(string Body);
public record UpdatePostRequest(string Body);

// Response DTOs
public record CategoryResponse(
    int Id, string Name, string Slug, string Description,
    int DisplayOrder, bool IsSystem, int ThreadCount, DateTimeOffset? LastActivityAt);

public record ThreadSummaryResponse(
    int Id, string Title, string Slug, bool IsPinned, bool IsLocked,
    Guid AuthorId, int ReplyCount, int VoteCount,
    DateTimeOffset CreatedAt, DateTimeOffset LastPostAt);

public record ThreadDetailResponse(
    int Id, string Title, string Slug, int CategoryId, string CategoryName, string CategorySlug,
    bool IsPinned, bool IsLocked, Guid AuthorId,
    DateTimeOffset CreatedAt, DateTimeOffset LastPostAt);

public record PostResponse(
    long Id, Guid AuthorId, string? Body, bool IsEdited, bool IsDeleted,
    int VoteCount, bool CurrentUserVoted,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
