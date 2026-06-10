using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Soverance.Messaging.Models;
using Soverance.Messaging.Services;
using Xunit;

namespace Soverance.Messaging.Tests;

public class MessagingServiceTests
{
    private static (TestDbContext db, SqliteConnection conn) NewDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(conn).Options;
        var db = new TestDbContext(options);
        db.Database.EnsureCreated();
        return (db, conn);
    }

    private static MessagingService NewService(TestDbContext db) =>
        new MessagingService(db, new NotificationService(db));

    [Fact]
    public async Task SendMessage_creates_conversation_message_and_notifies_recipient()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var notifications = new NotificationService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();

        var result = await svc.SendMessageAsync(a, b, "hello");

        Assert.False(result.Blocked);
        Assert.NotNull(result.Message);
        Assert.Equal("hello", result.Message!.Body);
        Assert.Equal(1, await notifications.GetUnreadCountAsync(b));
        Assert.Equal(0, await notifications.GetUnreadCountAsync(a));
    }

    [Fact]
    public async Task SendMessage_is_idempotent_on_conversation_for_same_pair_regardless_of_order()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();

        var r1 = await svc.SendMessageAsync(a, b, "one");
        var r2 = await svc.SendMessageAsync(b, a, "two");

        Assert.Equal(r1.ConversationId, r2.ConversationId);
        Assert.Single(await db.Set<Conversation>().ToListAsync());
    }

    [Fact]
    public async Task SendMessage_rejects_self_and_blank()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendMessageAsync(a, a, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendMessageAsync(a, Guid.NewGuid(), "  "));
    }

    [Fact]
    public async Task UnreadCount_counts_conversations_with_unread_then_clears_on_read()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var sent = await svc.SendMessageAsync(a, b, "hi");

        Assert.Equal(1, await svc.GetUnreadConversationCountAsync(b));
        Assert.Equal(0, await svc.GetUnreadConversationCountAsync(a));

        await svc.GetConversationAsync(b, sent.ConversationId);
        Assert.Equal(0, await svc.GetUnreadConversationCountAsync(b));
    }

    [Fact]
    public async Task GetConversation_returns_null_for_non_participant()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var stranger = Guid.NewGuid();
        var sent = await svc.SendMessageAsync(a, b, "hi");

        Assert.Null(await svc.GetConversationAsync(stranger, sent.ConversationId));
        Assert.NotNull(await svc.GetConversationAsync(a, sent.ConversationId));
    }

    [Fact]
    public async Task ListConversations_excludes_hidden_and_orders_by_last_message()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await svc.SendMessageAsync(a, b, "hi");

        var listB = await svc.ListConversationsAsync(b);
        Assert.Single(listB.Items);
        Assert.Equal(a, listB.Items[0].OtherUserId);
        Assert.Equal(1, listB.Items[0].UnreadCount);
    }

    [Fact]
    public async Task Block_prevents_recipient_block_send_and_hides_blocker_inbox()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await svc.SendMessageAsync(a, b, "hi");

        await svc.BlockUserAsync(a, b);

        var blocked = await svc.SendMessageAsync(b, a, "please respond");
        Assert.True(blocked.Blocked);
        Assert.Null(blocked.Message);

        Assert.Empty((await svc.ListConversationsAsync(a)).Items);
    }

    [Fact]
    public async Task Blocker_can_still_initiate_and_send_to_blocked_user()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await svc.BlockUserAsync(a, b);

        var sent = await svc.SendMessageAsync(a, b, "I can still reach you");
        Assert.False(sent.Blocked);
        Assert.NotNull(sent.Message);
    }

    [Fact]
    public async Task Unblock_restores_sending()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await svc.BlockUserAsync(a, b);
        Assert.True((await svc.SendMessageAsync(b, a, "x")).Blocked);

        await svc.UnblockUserAsync(a, b);
        Assert.False((await svc.SendMessageAsync(b, a, "y")).Blocked);
    }

    [Fact]
    public async Task Report_records_open_row_for_participant_only()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var stranger = Guid.NewGuid();
        var sent = await svc.SendMessageAsync(a, b, "hi");

        Assert.False(await svc.ReportAsync(stranger, sent.ConversationId, null, "spam"));
        Assert.True(await svc.ReportAsync(b, sent.ConversationId, sent.Message!.Id, "spam"));
        var report = await db.Set<Soverance.Messaging.Models.ConversationReport>().FirstAsync();
        Assert.Equal(Soverance.Messaging.Models.ReportStatus.Open, report.Status);
    }

    [Fact]
    public async Task Blocked_send_with_no_prior_conversation_does_not_create_one()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await svc.BlockUserAsync(b, a); // b blocks a; a has no prior conversation with b

        var result = await svc.SendMessageAsync(a, b, "hi");

        Assert.True(result.Blocked);
        Assert.Empty(await db.Set<Conversation>().ToListAsync()); // no ghost conversation created
    }

    [Fact]
    public async Task Unblock_restores_hidden_conversation_in_inbox()
    {
        var (db, conn) = NewDb(); using var _ = conn;
        var svc = NewService(db);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await svc.SendMessageAsync(a, b, "hi");
        await svc.BlockUserAsync(a, b);
        Assert.Empty((await svc.ListConversationsAsync(a)).Items); // hidden while blocked

        await svc.UnblockUserAsync(a, b);
        Assert.Single((await svc.ListConversationsAsync(a)).Items); // restored on unblock
    }
}
