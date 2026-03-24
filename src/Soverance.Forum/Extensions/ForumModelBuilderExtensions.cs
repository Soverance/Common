using Microsoft.EntityFrameworkCore;
using Soverance.Forum.Models;

namespace Soverance.Forum.Extensions;

public static class ForumModelBuilderExtensions
{
    public static ModelBuilder ApplyForumConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ForumCategory).Assembly);
        return modelBuilder;
    }
}
