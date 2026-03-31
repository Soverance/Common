using Soverance.Forum.DTOs;

namespace Soverance.Forum.Services;

public interface IForumService
{
    // Categories
    Task<List<CategoryResponse>> GetCategoriesAsync();
    Task<CategoryResponse?> GetCategoryBySlugAsync(string slug);
    Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryResponse?> UpdateCategoryAsync(int id, UpdateCategoryRequest request);
    Task<bool> DeleteCategoryAsync(int id);

    // Threads
    Task<(List<ThreadSummaryResponse> Threads, bool HasMore)> GetThreadsAsync(
        string categorySlug, long? afterLastPostAtTicks = null, int? afterId = null, int limit = 25, bool isModerator = false);
    Task<ThreadDetailResponse?> GetThreadBySlugAsync(string categorySlug, string threadSlug, bool isModerator = false);
    Task<ThreadDetailResponse?> CreateThreadAsync(
        string categorySlug, CreateThreadRequest request, Guid authorId);
    Task<bool> TogglePinAsync(int threadId);
    Task<bool> ToggleLockAsync(int threadId);
    Task<bool> DeleteThreadAsync(int threadId, Guid callerId, bool isModerator);
    Task<bool> RestoreThreadAsync(int threadId);
    Task<bool> PurgeThreadAsync(int threadId, Func<string, Task>? deleteAttachment = null);

    // Posts
    Task<(List<PostResponse> Posts, bool HasMore)> GetPostsAsync(
        int threadId, long? afterId = null, int limit = 25, Guid? currentUserId = null, bool isModerator = false);
    Task<PostResponse?> CreatePostAsync(
        int threadId, CreatePostRequest request, Guid authorId);
    Task<PostResponse?> UpdatePostAsync(long postId, UpdatePostRequest request, Guid callerId, bool isModerator);
    Task<bool> DeletePostAsync(long postId, Guid callerId, bool isModerator);
    Task<PurgeResult> PurgePostAsync(long postId, Func<string, Task>? deleteAttachment = null);

    // Voting
    Task<(int VoteCount, bool UserVoted)> ToggleVoteAsync(long postId, Guid userId);
}
