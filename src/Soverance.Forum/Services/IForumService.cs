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
        string categorySlug, long? afterLastPostAtTicks = null, int? afterId = null, int limit = 25);
    Task<ThreadDetailResponse?> GetThreadBySlugAsync(string categorySlug, string threadSlug);
    Task<ThreadDetailResponse?> CreateThreadAsync(
        string categorySlug, CreateThreadRequest request, Guid authorId);
    Task<bool> TogglePinAsync(int threadId);
    Task<bool> ToggleLockAsync(int threadId);

    // Posts
    Task<(List<PostResponse> Posts, bool HasMore)> GetPostsAsync(
        int threadId, long? afterId = null, int limit = 25, Guid? currentUserId = null);
    Task<PostResponse?> CreatePostAsync(
        int threadId, CreatePostRequest request, Guid authorId);
    Task<PostResponse?> UpdatePostAsync(long postId, UpdatePostRequest request, Guid callerId, bool isModerator);
    Task<bool> DeletePostAsync(long postId, Guid callerId, bool isModerator);

    // Voting
    Task<(int VoteCount, bool UserVoted)> ToggleVoteAsync(long postId, Guid userId);
}
