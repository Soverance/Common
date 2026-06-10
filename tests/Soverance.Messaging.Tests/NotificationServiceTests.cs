using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Soverance.Messaging.Models;
using Soverance.Messaging.Services;
using Xunit;

namespace Soverance.Messaging.Tests;

public class NotificationServiceTests
{
    private static (TestDbContext db, SqliteConnection conn) NewDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(conn).Options;
        var db = new TestDbContext(options);
        db.Database.EnsureCreated();
        return (db, conn);
    }

    [Fact]
    public async Task CreateAsync_persists_and_GetUnreadCount_counts_it()
    {
        var (db, conn) = NewDb();
        using var _ = conn;
        var svc = new NotificationService(db);
        var recipient = Guid.NewGuid();

        var id = await svc.CreateAsync(recipient, NotificationType.ForumReply,
            Guid.NewGuid(), "/forum/thread/abc#post-1", "Re: your thread");

        Assert.NotNull(id);
        Assert.Equal(1, await svc.GetUnreadCountAsync(recipient));
    }

    [Fact]
    public async Task CreateAsync_skips_self_notification()
    {
        var (db, conn) = NewDb();
        using var _ = conn;
        var svc = new NotificationService(db);
        var me = Guid.NewGuid();

        var id = await svc.CreateAsync(me, NotificationType.ForumReply, me, "/x", null);

        Assert.Null(id);
        Assert.Equal(0, await svc.GetUnreadCountAsync(me));
    }

    [Fact]
    public async Task MarkReadAsync_clears_unread_for_owner_only()
    {
        var (db, conn) = NewDb();
        using var _ = conn;
        var svc = new NotificationService(db);
        var me = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var id = (await svc.CreateAsync(me, NotificationType.ForumReaction, Guid.NewGuid(), "/x", null))!.Value;

        Assert.False(await svc.MarkReadAsync(stranger, id)); // not my notification
        Assert.Equal(1, await svc.GetUnreadCountAsync(me));
        Assert.True(await svc.MarkReadAsync(me, id));
        Assert.Equal(0, await svc.GetUnreadCountAsync(me));
    }

    [Fact]
    public async Task ListAsync_returns_newest_first_with_unreadOnly_filter()
    {
        var (db, conn) = NewDb();
        using var _ = conn;
        var svc = new NotificationService(db);
        var me = Guid.NewGuid();
        var a = (await svc.CreateAsync(me, NotificationType.ForumReply, Guid.NewGuid(), "/a", null))!.Value;
        var b = (await svc.CreateAsync(me, NotificationType.ForumReply, Guid.NewGuid(), "/b", null))!.Value;
        await svc.MarkReadAsync(me, a);

        var all = await svc.ListAsync(me);
        Assert.Equal(b, all.Items[0].Id); // newest first

        var unread = await svc.ListAsync(me, unreadOnly: true);
        Assert.Single(unread.Items);
        Assert.Equal(b, unread.Items[0].Id);
    }

    [Fact]
    public async Task MarkAllReadAsync_clears_every_unread_for_user()
    {
        var (db, conn) = NewDb();
        using var _ = conn;
        var svc = new NotificationService(db);
        var me = Guid.NewGuid();
        await svc.CreateAsync(me, NotificationType.ForumReply, Guid.NewGuid(), "/a", null);
        await svc.CreateAsync(me, NotificationType.ForumReaction, Guid.NewGuid(), "/b", null);

        var cleared = await svc.MarkAllReadAsync(me);

        Assert.Equal(2, cleared);
        Assert.Equal(0, await svc.GetUnreadCountAsync(me));
    }

    [Fact]
    public async Task ListAsync_paging_reports_HasMore_and_supports_beforeId_cursor()
    {
        var (db, conn) = NewDb();
        using var _ = conn;
        var svc = new NotificationService(db);
        var me = Guid.NewGuid();
        long lastId = 0;
        for (var i = 0; i < 3; i++)
            lastId = (await svc.CreateAsync(me, NotificationType.ForumReply, Guid.NewGuid(), $"/{i}", null))!.Value;

        var firstPage = await svc.ListAsync(me, limit: 2);
        Assert.True(firstPage.HasMore);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(lastId, firstPage.Items[0].Id); // newest first

        var cursor = firstPage.Items[^1].Id;
        var secondPage = await svc.ListAsync(me, limit: 2, beforeId: cursor);
        Assert.False(secondPage.HasMore);
        Assert.Single(secondPage.Items);
        Assert.True(secondPage.Items[0].Id < cursor);
    }
}
