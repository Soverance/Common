using Microsoft.EntityFrameworkCore;
using Soverance.Messaging.DTOs;
using Soverance.Messaging.Models;

namespace Soverance.Messaging.Services;

public class MessagingService : IMessagingService
{
    private readonly DbContext _db;
    private readonly INotificationService _notifications;

    public MessagingService(DbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    private static (Guid a, Guid b) Canonical(Guid x, Guid y) =>
        x.CompareTo(y) <= 0 ? (x, y) : (y, x);

    private static string Preview(string body) =>
        body.Length <= 140 ? body : body[..140];

    public async Task<SendMessageResult> SendMessageAsync(Guid me, Guid other, string body)
    {
        if (me == other) throw new ArgumentException("Cannot message yourself.", nameof(other));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Message body is required.", nameof(body));

        var blocked = await _db.Set<UserBlock>()
            .AnyAsync(b => b.BlockerUserId == other && b.BlockedUserId == me);
        if (blocked)
        {
            // Don't materialize a conversation just to report blocked status.
            var (bua, bub) = Canonical(me, other);
            var existingId = await _db.Set<Conversation>()
                .Where(c => c.UserAId == bua && c.UserBId == bub)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync() ?? 0;
            return new SendMessageResult(null, existingId, Blocked: true);
        }

        var conversation = await GetOrCreateConversationAsync(me, other);
        var now = DateTimeOffset.UtcNow;

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = me,
            Body = body.Trim(),
            CreatedAt = now,
        };
        _db.Set<Message>().Add(message);
        conversation.LastMessageAt = now;

        var participants = await _db.Set<ConversationParticipant>()
            .Where(p => p.ConversationId == conversation.Id).ToListAsync();
        foreach (var p in participants)
        {
            p.IsHidden = false;
            if (p.UserId == me) p.LastReadAt = now;
        }

        await _db.SaveChangesAsync();

        try
        {
            await _notifications.CreateAsync(
                other, NotificationType.MessageReceived, me,
                $"/messages/{conversation.Id}", Preview(message.Body));
        }
        catch { /* notifications are best-effort */ }

        return new SendMessageResult(
            new MessageDto(message.Id, conversation.Id, me, message.Body, message.CreatedAt),
            conversation.Id, Blocked: false);
    }

    private async Task<Conversation> GetOrCreateConversationAsync(Guid me, Guid other)
    {
        var (ua, ub) = Canonical(me, other);
        var existing = await _db.Set<Conversation>()
            .FirstOrDefaultAsync(c => c.UserAId == ua && c.UserBId == ub);
        if (existing != null) return existing;

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            UserAId = ua, UserBId = ub, CreatedAt = now, LastMessageAt = now,
            Participants =
            {
                new ConversationParticipant { UserId = ua, IsHidden = false },
                new ConversationParticipant { UserId = ub, IsHidden = false },
            },
        };
        _db.Set<Conversation>().Add(conversation);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            existing = await _db.Set<Conversation>()
                .FirstOrDefaultAsync(c => c.UserAId == ua && c.UserBId == ub);
            if (existing == null) throw;
            return existing;
        }
        return conversation;
    }

    public async Task<ConversationListResult> ListConversationsAsync(Guid me, int limit = 25, DateTimeOffset? before = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        var q = _db.Set<ConversationParticipant>()
            .Where(p => p.UserId == me && !p.IsHidden);
        if (before.HasValue)
            q = q.Where(p => p.Conversation.LastMessageAt < before.Value);

        var rows = await q
            .OrderByDescending(p => p.Conversation.LastMessageAt)
            .Take(limit + 1)
            .Select(p => new
            {
                p.ConversationId,
                p.Conversation.LastMessageAt,
                p.LastReadAt,
                OtherUserId = p.Conversation.UserAId == me ? p.Conversation.UserBId : p.Conversation.UserAId,
                LastBody = p.Conversation.Messages
                    .OrderByDescending(m => m.Id).Select(m => m.Body).FirstOrDefault(),
                UnreadCount = p.Conversation.Messages
                    .Count(m => m.SenderId != me && (p.LastReadAt == null || m.CreatedAt > p.LastReadAt)),
            })
            .ToListAsync();

        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var items = rows.Select(r => new ConversationListItem(
            r.ConversationId, r.OtherUserId,
            r.LastBody == null ? null : Preview(r.LastBody),
            r.LastMessageAt, r.UnreadCount)).ToList();
        return new ConversationListResult(items, hasMore);
    }

    public async Task<ConversationDetail?> GetConversationAsync(Guid me, int conversationId, int limit = 50, long? beforeId = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        var conversation = await _db.Set<Conversation>()
            .FirstOrDefaultAsync(c => c.Id == conversationId && (c.UserAId == me || c.UserBId == me));
        if (conversation == null) return null;

        var mq = _db.Set<Message>().Where(m => m.ConversationId == conversationId);
        if (beforeId.HasValue) mq = mq.Where(m => m.Id < beforeId.Value);
        var page = await mq.OrderByDescending(m => m.Id).Take(limit + 1)
            .Select(m => new MessageDto(m.Id, m.ConversationId, m.SenderId, m.Body, m.CreatedAt))
            .ToListAsync();
        var hasMore = page.Count > limit;
        if (hasMore) page.RemoveAt(page.Count - 1);
        page.Reverse();

        await MarkReadAsync(me, conversationId);

        var other = conversation.UserAId == me ? conversation.UserBId : conversation.UserAId;
        return new ConversationDetail(conversationId, other, page, hasMore);
    }

    public async Task<bool> MarkReadAsync(Guid me, int conversationId)
    {
        var participant = await _db.Set<ConversationParticipant>()
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == me);
        if (participant == null) return false;
        participant.LastReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetUnreadConversationCountAsync(Guid me)
    {
        return await _db.Set<ConversationParticipant>()
            .Where(p => p.UserId == me && !p.IsHidden)
            .CountAsync(p => p.Conversation.Messages
                .Any(m => m.SenderId != me && (p.LastReadAt == null || m.CreatedAt > p.LastReadAt)));
    }

    public async Task<bool> BlockUserAsync(Guid me, Guid other)
    {
        if (me == other) return false;
        var exists = await _db.Set<UserBlock>()
            .AnyAsync(b => b.BlockerUserId == me && b.BlockedUserId == other);
        if (!exists)
        {
            _db.Set<UserBlock>().Add(new UserBlock
            {
                BlockerUserId = me, BlockedUserId = other, CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        var (ua, ub) = Canonical(me, other);
        var convo = await _db.Set<Conversation>().FirstOrDefaultAsync(c => c.UserAId == ua && c.UserBId == ub);
        if (convo != null)
        {
            var mine = await _db.Set<ConversationParticipant>()
                .FirstOrDefaultAsync(p => p.ConversationId == convo.Id && p.UserId == me);
            if (mine != null) mine.IsHidden = true;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnblockUserAsync(Guid me, Guid other)
    {
        var block = await _db.Set<UserBlock>()
            .FirstOrDefaultAsync(b => b.BlockerUserId == me && b.BlockedUserId == other);
        if (block == null) return false;
        _db.Set<UserBlock>().Remove(block);

        // Restore visibility of the conversation that BlockUserAsync hid for the blocker.
        var (ua, ub) = Canonical(me, other);
        var convo = await _db.Set<Conversation>().FirstOrDefaultAsync(c => c.UserAId == ua && c.UserBId == ub);
        if (convo != null)
        {
            var mine = await _db.Set<ConversationParticipant>()
                .FirstOrDefaultAsync(p => p.ConversationId == convo.Id && p.UserId == me);
            if (mine != null) mine.IsHidden = false;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReportAsync(Guid me, int conversationId, long? messageId, string? note)
    {
        var isParticipant = await _db.Set<Conversation>()
            .AnyAsync(c => c.Id == conversationId && (c.UserAId == me || c.UserBId == me));
        if (!isParticipant) return false;
        _db.Set<ConversationReport>().Add(new ConversationReport
        {
            ReporterUserId = me, ConversationId = conversationId, MessageId = messageId,
            Note = note, Status = ReportStatus.Open, CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        return true;
    }
}
