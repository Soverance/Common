using Soverance.Messaging.DTOs;

namespace Soverance.Messaging.Services;

public interface INotificationService
{
    /// <summary>Creates a notification. No-op (returns null) when actorUserId == recipientUserId.</summary>
    Task<long?> CreateAsync(
        Guid recipientUserId, string type, Guid? actorUserId,
        string targetUrl, string? snippet);

    Task<int> GetUnreadCountAsync(Guid userId);
    Task<NotificationListResult> ListAsync(Guid userId, int limit = 25, long? beforeId = null, bool unreadOnly = false);
    /// <summary>Marks a notification read. Returns false if it doesn't exist or isn't owned by the user; true if found and owned (idempotent — also true when already read).</summary>
    Task<bool> MarkReadAsync(Guid userId, long notificationId);
    Task<int> MarkAllReadAsync(Guid userId);
}
