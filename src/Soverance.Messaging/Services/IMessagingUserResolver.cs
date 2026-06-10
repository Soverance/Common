namespace Soverance.Messaging.Services;

// TODO: implemented at the Api (bridges Guid -> user identity) and consumed when the notification/message
// list endpoints are enriched with actor display names. Not wired into NotificationService itself.
public interface IMessagingUserResolver
{
    Task<Dictionary<Guid, MessagingUserInfo>> ResolveUsersAsync(IEnumerable<Guid> userIds);
}

public record MessagingUserInfo(
    Guid UserId, string Username, string? DisplayName, string? AvatarUrl);
