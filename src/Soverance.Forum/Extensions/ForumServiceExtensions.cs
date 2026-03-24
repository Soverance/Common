using Microsoft.Extensions.DependencyInjection;
using Soverance.Forum.Services;

namespace Soverance.Forum.Extensions;

public static class ForumServiceExtensions
{
    public static IServiceCollection AddForumServices(this IServiceCollection services)
    {
        services.AddScoped<IForumService, ForumService>();
        return services;
    }
}
