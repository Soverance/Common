namespace Soverance.Forum.Services;

public interface IForumAuthorResolver
{
    Task<Dictionary<Guid, ForumAuthorInfo>> ResolveAuthorsAsync(IEnumerable<Guid> authorIds);
}

public record ForumAuthorInfo(
    Guid UserId, string Username, string? DisplayName,
    string? AvatarHash, int PostCount, DateTimeOffset JoinedAt);
