using Microsoft.Extensions.DependencyInjection;
using Soverance.Messaging.Services;

namespace Soverance.Messaging.Extensions;

public static class MessagingServiceExtensions
{
    public static IServiceCollection AddMessagingServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
