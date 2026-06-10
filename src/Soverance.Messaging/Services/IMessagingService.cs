using Soverance.Messaging.DTOs;

namespace Soverance.Messaging.Services;

public interface IMessagingService
{
    Task<SendMessageResult> SendMessageAsync(Guid me, Guid other, string body);
    Task<ConversationListResult> ListConversationsAsync(Guid me, int limit = 25, DateTimeOffset? before = null);
    Task<ConversationDetail?> GetConversationAsync(Guid me, int conversationId, int limit = 50, long? beforeId = null);
    Task<bool> MarkReadAsync(Guid me, int conversationId);
    Task<int> GetUnreadConversationCountAsync(Guid me);
    Task<bool> BlockUserAsync(Guid me, Guid other);
    Task<bool> UnblockUserAsync(Guid me, Guid other);
    Task<bool> ReportAsync(Guid me, int conversationId, long? messageId, string? note);
}
