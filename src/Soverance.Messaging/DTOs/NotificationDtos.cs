namespace Soverance.Messaging.DTOs;

public record NotificationResponse(
    long Id,
    string Type,
    Guid? ActorUserId,
    string TargetUrl,
    string? Snippet,
    bool IsRead,
    DateTimeOffset CreatedAt);

public record NotificationListResult(
    IReadOnlyList<NotificationResponse> Items,
    bool HasMore);
