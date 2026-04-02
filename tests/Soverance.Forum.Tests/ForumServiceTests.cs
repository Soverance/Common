using Microsoft.EntityFrameworkCore;
using Soverance.Forum.DTOs;
using Soverance.Forum.Models;
using Soverance.Forum.Services;
using Xunit;

namespace Soverance.Forum.Tests;

public class ForumServiceTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly ForumService _service;

    public ForumServiceTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new TestDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _service = new ForumService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // === Category Tests ===

    [Fact]
    public async Task CreateCategory_ReturnsCategory()
    {
        var result = await _service.CreateCategoryAsync(
            new CreateCategoryRequest("Bug Reports", "Report bugs here", 1));

        Assert.Equal("Bug Reports", result.Name);
        Assert.Equal("bug-reports", result.Slug);
        Assert.Equal("Report bugs here", result.Description);
        Assert.Equal(1, result.DisplayOrder);
        Assert.Equal(0, result.ThreadCount);
    }

    [Fact]
    public async Task CreateCategory_DuplicateName_AppendsSuffix()
    {
        await _service.CreateCategoryAsync(new CreateCategoryRequest("General", "First"));
        var second = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", "Second"));

        Assert.Equal("general-2", second.Slug);
    }

    [Fact]
    public async Task GetCategories_ReturnsOrderedList()
    {
        await _service.CreateCategoryAsync(new CreateCategoryRequest("Zebra", "Z", 2));
        await _service.CreateCategoryAsync(new CreateCategoryRequest("Alpha", "A", 1));

        var result = await _service.GetCategoriesAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal("Zebra", result[1].Name);
    }

    [Fact]
    public async Task UpdateCategory_UpdatesFields()
    {
        var created = await _service.CreateCategoryAsync(new CreateCategoryRequest("Old", "Old desc"));
        var updated = await _service.UpdateCategoryAsync(created.Id,
            new UpdateCategoryRequest("New", "New desc", 5));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.Equal("New desc", updated.Description);
        Assert.Equal(5, updated.DisplayOrder);
    }

    [Fact]
    public async Task DeleteCategory_EmptyCategory_ReturnsTrue()
    {
        var created = await _service.CreateCategoryAsync(new CreateCategoryRequest("ToDelete", ""));
        var result = await _service.DeleteCategoryAsync(created.Id);
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteCategory_WithThreads_ReturnsFalse()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("HasThreads", ""));
        await _service.CreateThreadAsync(category.Slug,
            new CreateThreadRequest("Thread", "Body"), Guid.NewGuid());

        var result = await _service.DeleteCategoryAsync(category.Id);
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteCategory_NotFound_ReturnsFalse()
    {
        var result = await _service.DeleteCategoryAsync(999);
        Assert.False(result);
    }

    // === Thread Tests ===

    [Fact]
    public async Task CreateThread_CreatesThreadAndFirstPost()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();

        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("My Thread", "Hello world"), authorId);

        Assert.NotNull(thread);
        Assert.Equal("My Thread", thread!.Title);
        Assert.Equal("my-thread", thread.Slug);
        Assert.Equal(authorId, thread.AuthorId);

        var (posts, _) = await _service.GetPostsAsync(thread.Id);
        Assert.Single(posts);
        Assert.Equal("Hello world", posts[0].Body);
        Assert.Equal(authorId, posts[0].AuthorId);
    }

    [Fact]
    public async Task CreateThread_SlugCollisionInSameCategory_AppendsSuffix()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();

        var first = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("Test", "Body"), authorId);
        var second = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("Test", "Body 2"), authorId);

        Assert.Equal("test", first!.Slug);
        Assert.Equal("test-2", second!.Slug);
    }

    [Fact]
    public async Task CreateThread_SameSlugDifferentCategory_Allowed()
    {
        var cat1 = await _service.CreateCategoryAsync(new CreateCategoryRequest("Cat 1", ""));
        var cat2 = await _service.CreateCategoryAsync(new CreateCategoryRequest("Cat 2", ""));
        var authorId = Guid.NewGuid();

        var t1 = await _service.CreateThreadAsync(cat1.Slug, new CreateThreadRequest("Test", "Body"), authorId);
        var t2 = await _service.CreateThreadAsync(cat2.Slug, new CreateThreadRequest("Test", "Body"), authorId);

        Assert.Equal("test", t1!.Slug);
        Assert.Equal("test", t2!.Slug);
    }

    [Fact]
    public async Task GetThreads_PinnedFirst()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();

        var normal = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("Normal", "Body"), authorId);
        var pinned = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("Pinned", "Body"), authorId);

        await _service.TogglePinAsync(pinned!.Id);

        var (threads, _) = await _service.GetThreadsAsync(category.Slug);

        Assert.Equal("Pinned", threads[0].Title);
        Assert.True(threads[0].IsPinned);
        Assert.Equal("Normal", threads[1].Title);
    }

    [Fact]
    public async Task TogglePin_TogglesState()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        await _service.TogglePinAsync(thread!.Id);
        var detail = await _service.GetThreadBySlugAsync(category.Slug, thread.Slug);
        Assert.True(detail!.IsPinned);

        await _service.TogglePinAsync(thread.Id);
        detail = await _service.GetThreadBySlugAsync(category.Slug, thread.Slug);
        Assert.False(detail!.IsPinned);
    }

    [Fact]
    public async Task ToggleLock_TogglesState()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        await _service.ToggleLockAsync(thread!.Id);
        var detail = await _service.GetThreadBySlugAsync(category.Slug, thread.Slug);
        Assert.True(detail!.IsLocked);
    }

    // === Post Tests ===

    [Fact]
    public async Task CreatePost_AddsReplyAndUpdatesLastPostAt()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "First"), Guid.NewGuid());

        var replyAuthor = Guid.NewGuid();
        var reply = await _service.CreatePostAsync(
            thread!.Id, new CreatePostRequest("Reply!"), replyAuthor);

        Assert.NotNull(reply);
        Assert.Equal("Reply!", reply!.Body);
        Assert.Equal(replyAuthor, reply.AuthorId);

        var updatedThread = await _service.GetThreadBySlugAsync(category.Slug, thread.Slug);
        Assert.True(updatedThread!.LastPostAt >= thread.LastPostAt);
    }

    [Fact]
    public async Task CreatePost_OnLockedThread_ReturnsNull()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        await _service.ToggleLockAsync(thread!.Id);

        var result = await _service.CreatePostAsync(
            thread.Id, new CreatePostRequest("Nope"), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdatePost_ByAuthor_SetsIsEdited()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "Original"), authorId);

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var post = posts[0];

        var updated = await _service.UpdatePostAsync(
            post.Id, new UpdatePostRequest("Edited"), authorId, false);

        Assert.NotNull(updated);
        Assert.Equal("Edited", updated!.Body);
        Assert.True(updated.IsEdited);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdatePost_ByOtherUser_ReturnsNull()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), authorId);

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);

        var result = await _service.UpdatePostAsync(
            posts[0].Id, new UpdatePostRequest("Hacked"), Guid.NewGuid(), false);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdatePost_ByModerator_Succeeds()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);

        var result = await _service.UpdatePostAsync(
            posts[0].Id, new UpdatePostRequest("Moderated"), Guid.NewGuid(), true);

        Assert.NotNull(result);
        Assert.Equal("Moderated", result!.Body);
    }

    [Fact]
    public async Task DeletePost_SoftDeletes_StripsBodyInResponse()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "Secret content"), authorId);

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var postId = posts[0].Id;

        var deleted = await _service.DeletePostAsync(postId, authorId, false);
        Assert.True(deleted);

        var (postsAfter, _) = await _service.GetPostsAsync(thread.Id, isModerator: true);
        Assert.Single(postsAfter);
        Assert.True(postsAfter[0].IsDeleted);
        Assert.Null(postsAfter[0].Body);
    }

    [Fact]
    public async Task DeletePost_RecordsDeletedBy()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), authorId);

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);

        await _service.DeletePostAsync(posts[0].Id, moderatorId, true);

        var post = await _db.Set<ForumPost>().FindAsync(posts[0].Id);
        Assert.Equal(moderatorId, post!.DeletedBy);
    }

    [Fact]
    public async Task DeletePost_ByOtherNonModerator_ReturnsFalse()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);

        var result = await _service.DeletePostAsync(posts[0].Id, Guid.NewGuid(), false);
        Assert.False(result);
    }

    // === Reaction Tests ===

    [Fact]
    public async Task ToggleReaction_AddsReaction()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var userId = Guid.NewGuid();

        var (reactions, userReactions) = await _service.ToggleReactionAsync(posts[0].Id, userId, "like");

        Assert.Equal(1, reactions.Like);
        Assert.Equal(0, reactions.Thanks);
        Assert.Equal(0, reactions.Funny);
        Assert.Contains("like", userReactions);
    }

    [Fact]
    public async Task ToggleReaction_RemovesExistingReaction()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var userId = Guid.NewGuid();

        await _service.ToggleReactionAsync(posts[0].Id, userId, "like");
        var (reactions, userReactions) = await _service.ToggleReactionAsync(posts[0].Id, userId, "like");

        Assert.Equal(0, reactions.Like);
        Assert.DoesNotContain("like", userReactions);
    }

    [Fact]
    public async Task ToggleReaction_MultipleTypes_TrackedSeparately()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var userId = Guid.NewGuid();

        await _service.ToggleReactionAsync(posts[0].Id, userId, "like");
        var (reactions, userReactions) = await _service.ToggleReactionAsync(posts[0].Id, userId, "thanks");

        Assert.Equal(1, reactions.Like);
        Assert.Equal(1, reactions.Thanks);
        Assert.Equal(0, reactions.Funny);
        Assert.Contains("like", userReactions);
        Assert.Contains("thanks", userReactions);
    }

    [Fact]
    public async Task ToggleReaction_MultipleUsers_CountsCorrectly()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var postId = posts[0].Id;

        await _service.ToggleReactionAsync(postId, Guid.NewGuid(), "like");
        await _service.ToggleReactionAsync(postId, Guid.NewGuid(), "like");
        var (reactions, _) = await _service.ToggleReactionAsync(postId, Guid.NewGuid(), "thanks");

        Assert.Equal(2, reactions.Like);
        Assert.Equal(1, reactions.Thanks);
    }

    [Fact]
    public async Task ToggleReaction_InvalidType_Throws()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ToggleReactionAsync(posts[0].Id, Guid.NewGuid(), "invalid"));
    }

    [Fact]
    public async Task GetPosts_IncludesUserReactions()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var userId = Guid.NewGuid();
        await _service.ToggleReactionAsync(posts[0].Id, userId, "like");
        await _service.ToggleReactionAsync(posts[0].Id, userId, "funny");

        var (postsWithReactions, _) = await _service.GetPostsAsync(thread.Id, currentUserId: userId);
        Assert.Equal(1, postsWithReactions[0].Reactions.Like);
        Assert.Equal(1, postsWithReactions[0].Reactions.Funny);
        Assert.Contains("like", postsWithReactions[0].UserReactions);
        Assert.Contains("funny", postsWithReactions[0].UserReactions);

        var (postsOtherUser, _) = await _service.GetPostsAsync(thread.Id, currentUserId: Guid.NewGuid());
        Assert.Empty(postsOtherUser[0].UserReactions);
    }

    // === Reply Tests ===

    [Fact]
    public async Task CreatePost_WithReplyToPostId_SetsReference()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "First"), authorId);

        var (posts, _) = await _service.GetPostsAsync(thread!.Id);
        var firstPostId = posts[0].Id;

        var reply = await _service.CreatePostAsync(
            thread.Id, new CreatePostRequest("Replying to you", firstPostId), Guid.NewGuid());

        Assert.NotNull(reply);
        Assert.Equal(firstPostId, reply!.ReplyToPostId);
    }

    [Fact]
    public async Task CreatePost_WithReplyToPostId_WrongThread_ReturnsNull()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();
        var thread1 = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T1", "First"), authorId);
        var thread2 = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T2", "Second"), authorId);

        var (posts1, _) = await _service.GetPostsAsync(thread1!.Id);

        var result = await _service.CreatePostAsync(
            thread2!.Id, new CreatePostRequest("Cross-thread reply", posts1[0].Id), authorId);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreatePost_WithReplyToPostId_NonexistentPost_ReturnsNull()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "B"), Guid.NewGuid());

        var result = await _service.CreatePostAsync(
            thread!.Id, new CreatePostRequest("Reply to nothing", 99999), Guid.NewGuid());

        Assert.Null(result);
    }

    // === Pagination Tests ===

    [Fact]
    public async Task GetPosts_Pagination_HasMore()
    {
        var category = await _service.CreateCategoryAsync(new CreateCategoryRequest("General", ""));
        var authorId = Guid.NewGuid();
        var thread = await _service.CreateThreadAsync(
            category.Slug, new CreateThreadRequest("T", "First"), authorId);

        for (var i = 0; i < 3; i++)
            await _service.CreatePostAsync(thread!.Id, new CreatePostRequest($"Reply {i}"), authorId);

        var (page1, hasMore1) = await _service.GetPostsAsync(thread!.Id, limit: 2);
        Assert.Equal(2, page1.Count);
        Assert.True(hasMore1);

        var (page2, hasMore2) = await _service.GetPostsAsync(thread.Id, afterId: page1[^1].Id, limit: 2);
        Assert.Equal(2, page2.Count);
        Assert.False(hasMore2);
    }
}
