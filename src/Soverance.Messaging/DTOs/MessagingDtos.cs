namespace Soverance.Messaging.DTOs;

public record ConversationListItem(
    int Id,
    Guid OtherUserId,
    string? LastMessagePreview,
    DateTimeOffset LastMessageAt,
    int UnreadCount);

public record ConversationListResult(
    IReadOnlyList<ConversationListItem> Items,
    bool HasMore);

public record MessageDto(
    long Id,
    int ConversationId,
    Guid SenderId,
    string Body,
    DateTimeOffset CreatedAt);

public record ConversationDetail(
    int Id,
    Guid OtherUserId,
    IReadOnlyList<MessageDto> Messages,
    bool HasMore);

public record SendMessageResult(MessageDto? Message, int ConversationId, bool Blocked);
