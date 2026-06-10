using Microsoft.EntityFrameworkCore;

namespace Soverance.Messaging.Extensions;

public static class MessagingModelBuilderExtensions
{
    public static ModelBuilder ApplyMessagingConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MessagingModelBuilderExtensions).Assembly);
        return modelBuilder;
    }
}
