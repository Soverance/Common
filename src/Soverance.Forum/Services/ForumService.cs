using Microsoft.EntityFrameworkCore;
using Soverance.Forum.DTOs;
using Soverance.Forum.Models;

namespace Soverance.Forum.Services;

public class ForumService : IForumService
{
    private readonly DbContext _db;

    public ForumService(DbContext db)
    {
        _db = db;
    }

    // === Categories ===

    public async Task<List<CategoryResponse>> GetCategoriesAsync()
    {
        return await _db.Set<ForumCategory>()
            .OrderByDescending(c => c.IsSystem)
            .ThenBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryResponse(
                c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder,
                c.IsSystem,
                c.Threads.Count,
                c.Threads.SelectMany(t => t.Posts).Max(p => (DateTimeOffset?)p.CreatedAt)))
            .ToListAsync();
    }

    public async Task<CategoryResponse?> GetCategoryBySlugAsync(string slug)
    {
        return await _db.Set<ForumCategory>()
            .Where(c => c.Slug == slug)
            .Select(c => new CategoryResponse(
                c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder,
                c.IsSystem,
                c.Threads.Count,
                c.Threads.SelectMany(t => t.Posts).Max(p => (DateTimeOffset?)p.CreatedAt)))
            .FirstOrDefaultAsync();
    }

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var slug = await GenerateUniqueCategorySlugAsync(request.Name);

        var category = new ForumCategory
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Set<ForumCategory>().Add(category);
        await _db.SaveChangesAsync();

        return new CategoryResponse(
            category.Id, category.Name, category.Slug, category.Description,
            category.DisplayOrder, false, 0, null);
    }

    public async Task<CategoryResponse?> UpdateCategoryAsync(int id, UpdateCategoryRequest request)
    {
        var category = await _db.Set<ForumCategory>().FindAsync(id);
        if (category == null) return null;

        category.Name = request.Name;
        category.Description = request.Description;
        category.DisplayOrder = request.DisplayOrder;

        await _db.SaveChangesAsync();

        var threadCount = await _db.Set<ForumThread>().CountAsync(t => t.CategoryId == id);
        var lastActivity = await _db.Set<ForumPost>()
            .Where(p => p.Thread.CategoryId == id)
            .MaxAsync(p => (DateTimeOffset?)p.CreatedAt);

        return new CategoryResponse(
            category.Id, category.Name, category.Slug, category.Description,
            category.DisplayOrder, category.IsSystem, threadCount, lastActivity);
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        var category = await _db.Set<ForumCategory>()
            .Include(c => c.Threads)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null || category.Threads.Count > 0) return false;

        _db.Set<ForumCategory>().Remove(category);
        await _db.SaveChangesAsync();
        return true;
    }

    // === Threads ===

    public async Task<(List<ThreadSummaryResponse> Threads, bool HasMore)> GetThreadsAsync(
        string categorySlug, long? afterLastPostAtTicks = null, int? afterId = null, int limit = 25, bool isModerator = false)
    {
        var category = await _db.Set<ForumCategory>()
            .FirstOrDefaultAsync(c => c.Slug == categorySlug);

        if (category == null) return ([], false);

        // Pinned threads first (always shown, not affected by cursor)
        var pinned = await _db.Set<ForumThread>()
            .Where(t => t.CategoryId == category.Id && t.IsPinned)
            .OrderByDescending(t => t.LastPostAt)
            .Select(t => new ThreadSummaryResponse(
                t.Id, t.Title, t.Slug, t.IsPinned, t.IsLocked,
                t.AuthorId,
                isModerator
                    ? t.Posts.Count - 1
                    : (t.Posts.Count(p => !p.IsDeleted) > 1 ? t.Posts.Count(p => !p.IsDeleted) - 1 : 0),
                isModerator ? t.Posts.SelectMany(p => p.Votes).Count() : t.Posts.Where(p => !p.IsDeleted).SelectMany(p => p.Votes).Count(),
                t.CreatedAt, t.LastPostAt))
            .ToListAsync();

        // Non-pinned threads with cursor pagination
        var query = _db.Set<ForumThread>()
            .Where(t => t.CategoryId == category.Id && !t.IsPinned);

        if (afterLastPostAtTicks != null && afterId != null)
        {
            var afterDate = new DateTimeOffset(afterLastPostAtTicks.Value, TimeSpan.Zero);
            query = query.Where(t =>
                t.LastPostAt < afterDate ||
                (t.LastPostAt == afterDate && t.Id < afterId.Value));
        }

        var threads = await query
            .OrderByDescending(t => t.LastPostAt)
            .ThenByDescending(t => t.Id)
            .Take(limit + 1)
            .Select(t => new ThreadSummaryResponse(
                t.Id, t.Title, t.Slug, t.IsPinned, t.IsLocked,
                t.AuthorId,
                isModerator
                    ? t.Posts.Count - 1
                    : (t.Posts.Count(p => !p.IsDeleted) > 1 ? t.Posts.Count(p => !p.IsDeleted) - 1 : 0),
                isModerator ? t.Posts.SelectMany(p => p.Votes).Count() : t.Posts.Where(p => !p.IsDeleted).SelectMany(p => p.Votes).Count(),
                t.CreatedAt, t.LastPostAt))
            .ToListAsync();

        var hasMore = threads.Count > limit;
        if (hasMore) threads = threads.Take(limit).ToList();

        // Only include pinned threads on the first page
        if (afterLastPostAtTicks == null)
            return ([.. pinned, .. threads], hasMore);

        return (threads, hasMore);
    }

    public async Task<ThreadDetailResponse?> GetThreadBySlugAsync(string categorySlug, string threadSlug)
    {
        return await _db.Set<ForumThread>()
            .Where(t => t.Category.Slug == categorySlug && t.Slug == threadSlug)
            .Select(t => new ThreadDetailResponse(
                t.Id, t.Title, t.Slug, t.CategoryId, t.Category.Name, t.Category.Slug,
                t.IsPinned, t.IsLocked, t.AuthorId,
                t.CreatedAt, t.LastPostAt))
            .FirstOrDefaultAsync();
    }

    public async Task<ThreadDetailResponse?> CreateThreadAsync(
        string categorySlug, CreateThreadRequest request, Guid authorId)
    {
        var category = await _db.Set<ForumCategory>()
            .FirstOrDefaultAsync(c => c.Slug == categorySlug);

        if (category == null) return null;

        var slug = await GenerateUniqueThreadSlugAsync(category.Id, request.Title);
        var now = DateTimeOffset.UtcNow;

        var thread = new ForumThread
        {
            CategoryId = category.Id,
            AuthorId = authorId,
            Title = request.Title,
            Slug = slug,
            CreatedAt = now,
            LastPostAt = now
        };

        _db.Set<ForumThread>().Add(thread);
        await _db.SaveChangesAsync();

        // Create first post
        var post = new ForumPost
        {
            ThreadId = thread.Id,
            AuthorId = authorId,
            Body = request.Body,
            CreatedAt = now
        };

        _db.Set<ForumPost>().Add(post);
        await _db.SaveChangesAsync();

        return new ThreadDetailResponse(
            thread.Id, thread.Title, thread.Slug, category.Id, category.Name, category.Slug,
            thread.IsPinned, thread.IsLocked, thread.AuthorId,
            thread.CreatedAt, thread.LastPostAt);
    }

    public async Task<bool> TogglePinAsync(int threadId)
    {
        var thread = await _db.Set<ForumThread>().FindAsync(threadId);
        if (thread == null) return false;

        thread.IsPinned = !thread.IsPinned;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleLockAsync(int threadId)
    {
        var thread = await _db.Set<ForumThread>().FindAsync(threadId);
        if (thread == null) return false;

        thread.IsLocked = !thread.IsLocked;
        await _db.SaveChangesAsync();
        return true;
    }

    // === Posts ===

    public async Task<(List<PostResponse> Posts, bool HasMore)> GetPostsAsync(
        int threadId, long? afterId = null, int limit = 25, Guid? currentUserId = null, bool isModerator = false)
    {
        var query = _db.Set<ForumPost>()
            .Where(p => p.ThreadId == threadId);

        if (!isModerator)
            query = query.Where(p => !p.IsDeleted);

        if (afterId != null)
            query = query.Where(p => p.Id > afterId.Value);

        var posts = await query
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(limit + 1)
            .Select(p => new PostResponse(
                p.Id,
                p.AuthorId,
                p.IsDeleted ? null : p.Body,
                p.IsEdited,
                p.IsDeleted,
                p.Votes.Count,
                currentUserId != null && p.Votes.Any(v => v.UserId == currentUserId.Value),
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync();

        var hasMore = posts.Count > limit;
        if (hasMore) posts = posts.Take(limit).ToList();

        return (posts, hasMore);
    }

    public async Task<PostResponse?> CreatePostAsync(
        int threadId, CreatePostRequest request, Guid authorId)
    {
        var thread = await _db.Set<ForumThread>().FindAsync(threadId);
        if (thread == null || thread.IsLocked) return null;

        var now = DateTimeOffset.UtcNow;
        var post = new ForumPost
        {
            ThreadId = threadId,
            AuthorId = authorId,
            Body = request.Body,
            CreatedAt = now
        };

        _db.Set<ForumPost>().Add(post);
        thread.LastPostAt = now;
        await _db.SaveChangesAsync();

        return new PostResponse(
            post.Id, post.AuthorId, post.Body, false, false, 0, false,
            post.CreatedAt, null);
    }

    public async Task<PostResponse?> UpdatePostAsync(
        long postId, UpdatePostRequest request, Guid callerId, bool isModerator)
    {
        var post = await _db.Set<ForumPost>()
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null || post.IsDeleted) return null;
        if (post.AuthorId != callerId && !isModerator) return null;

        post.Body = request.Body;
        post.IsEdited = true;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return new PostResponse(
            post.Id, post.AuthorId, post.Body, post.IsEdited, post.IsDeleted,
            post.Votes.Count,
            post.Votes.Any(v => v.UserId == callerId),
            post.CreatedAt, post.UpdatedAt);
    }

    public async Task<bool> DeletePostAsync(long postId, Guid callerId, bool isModerator)
    {
        var post = await _db.Set<ForumPost>().FindAsync(postId);
        if (post == null || post.IsDeleted) return false;
        if (post.AuthorId != callerId && !isModerator) return false;

        post.IsDeleted = true;
        post.DeletedBy = callerId;

        await _db.SaveChangesAsync();
        return true;
    }

    // === Voting ===

    public async Task<(int VoteCount, bool UserVoted)> ToggleVoteAsync(long postId, Guid userId)
    {
        var existing = await _db.Set<ForumVote>()
            .FirstOrDefaultAsync(v => v.PostId == postId && v.UserId == userId);

        if (existing != null)
        {
            _db.Set<ForumVote>().Remove(existing);
        }
        else
        {
            _db.Set<ForumVote>().Add(new ForumVote
            {
                PostId = postId,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        var voteCount = await _db.Set<ForumVote>().CountAsync(v => v.PostId == postId);
        var userVoted = existing == null;

        return (voteCount, userVoted);
    }

    public async Task<PurgeResult> PurgePostAsync(long postId, Func<string, Task>? deleteAttachment = null)
    {
        var post = await _db.Set<ForumPost>()
            .Include(p => p.Thread)
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null) return PurgeResult.NotFound;

        // Query the first post ID separately to avoid EF navigation fixup issues
        var firstPostId = await _db.Set<ForumPost>()
            .Where(p => p.ThreadId == post.ThreadId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => p.Id)
            .FirstAsync();
        var isFirstPost = post.Id == firstPostId;

        if (isFirstPost)
        {
            // Purge entire thread — collect all attachments first
            var allAttachments = await _db.Set<ForumAttachment>()
                .Where(a => a.Post != null && a.Post.ThreadId == post.ThreadId)
                .ToListAsync();

            if (deleteAttachment != null)
            {
                foreach (var att in allAttachments)
                    await deleteAttachment(att.StoragePath);
            }

            _db.Set<ForumAttachment>().RemoveRange(allAttachments);
            _db.Set<ForumThread>().Remove(post.Thread);
            await _db.SaveChangesAsync();
            return PurgeResult.ThreadPurged;
        }
        else
        {
            // Purge single post — delete its attachments
            if (deleteAttachment != null)
            {
                foreach (var att in post.Attachments)
                    await deleteAttachment(att.StoragePath);
            }

            _db.Set<ForumAttachment>().RemoveRange(post.Attachments);
            _db.Set<ForumPost>().Remove(post);
            await _db.SaveChangesAsync();
            return PurgeResult.PostPurged;
        }
    }

    // === Helpers ===

    private async Task<string> GenerateUniqueCategorySlugAsync(string name)
    {
        var baseSlug = SlugGenerator.Generate(name);
        var slug = baseSlug;
        var suffix = 1;

        while (await _db.Set<ForumCategory>().AnyAsync(c => c.Slug == slug))
        {
            suffix++;
            slug = SlugGenerator.AppendSuffix(baseSlug, suffix);
        }

        return slug;
    }

    private async Task<string> GenerateUniqueThreadSlugAsync(int categoryId, string title)
    {
        var baseSlug = SlugGenerator.Generate(title);
        var slug = baseSlug;
        var suffix = 1;

        while (await _db.Set<ForumThread>().AnyAsync(t => t.CategoryId == categoryId && t.Slug == slug))
        {
            suffix++;
            slug = SlugGenerator.AppendSuffix(baseSlug, suffix);
        }

        return slug;
    }
}
