namespace Soverance.Forum.Services;

public interface IForumAuthorResolver
{
    Task<Dictionary<Guid, ForumAuthorInfo>> ResolveAuthorsAsync(IEnumerable<Guid> authorIds);
}

public record ForumAuthorInfo(
    Guid UserId, string Username, string? AvatarHash,
    int PostCount, DateTimeOffset JoinedAt);
