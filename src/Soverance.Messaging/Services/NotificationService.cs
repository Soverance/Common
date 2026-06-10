using Microsoft.EntityFrameworkCore;
using Soverance.Messaging.DTOs;
using Soverance.Messaging.Models;

namespace Soverance.Messaging.Services;

public class NotificationService : INotificationService
{
    private readonly DbContext _db;

    public NotificationService(DbContext db)
    {
        _db = db;
    }

    public async Task<long?> CreateAsync(
        Guid recipientUserId, string type, Guid? actorUserId,
        string targetUrl, string? snippet)
    {
        if (actorUserId.HasValue && actorUserId.Value == recipientUserId)
            return null; // never notify yourself

        var n = new Notification
        {
            RecipientUserId = recipientUserId,
            Type = type,
            ActorUserId = actorUserId,
            TargetUrl = targetUrl,
            Snippet = snippet,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Set<Notification>().Add(n);
        await _db.SaveChangesAsync();
        return n.Id;
    }

    public Task<int> GetUnreadCountAsync(Guid userId) =>
        _db.Set<Notification>().CountAsync(n => n.RecipientUserId == userId && !n.IsRead);

    public async Task<NotificationListResult> ListAsync(
        Guid userId, int limit = 25, long? beforeId = null, bool unreadOnly = false)
    {
        limit = Math.Clamp(limit, 1, 100);
        var q = _db.Set<Notification>().Where(n => n.RecipientUserId == userId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        if (beforeId.HasValue) q = q.Where(n => n.Id < beforeId.Value);

        var rows = await q.OrderByDescending(n => n.Id)
            .Take(limit + 1)
            .Select(n => new NotificationResponse(
                n.Id, n.Type, n.ActorUserId, n.TargetUrl, n.Snippet, n.IsRead, n.CreatedAt))
            .ToListAsync();

        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new NotificationListResult(rows, hasMore);
    }

    public async Task<bool> MarkReadAsync(Guid userId, long notificationId)
    {
        var n = await _db.Set<Notification>()
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.RecipientUserId == userId);
        if (n is null) return false;
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId)
    {
        // Load-then-save mirrors ForumService convention; switch to ExecuteUpdateAsync if unread volumes grow large.
        var unread = await _db.Set<Notification>()
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync();
        var now = DateTimeOffset.UtcNow;
        foreach (var n in unread) { n.IsRead = true; n.ReadAt = now; }
        if (unread.Count > 0) await _db.SaveChangesAsync();
        return unread.Count;
    }
}
