using Microsoft.EntityFrameworkCore;
using Soverance.Forum.Models;

namespace Soverance.Forum.Services;

public static class ForumSeeder
{
    private static readonly (string Slug, string Name, string Description, int DisplayOrder, bool RequiresAdminForNewThreads)[] SystemCategories =
    [
        ("news",        "News",        "News updates about Vanalytics",         0, true),
        ("help",        "Help",        "Help with features in Vanalytics",      1, false),
        ("bugs",        "Bugs",        "Report bugs in Vanalytics",             2, false),
        ("suggestions", "Suggestions", "Request new features for Vanalytics",   3, false),
    ];

    public static async Task SeedSystemCategoriesAsync(DbContext db)
    {
        var existing = await db.Set<ForumCategory>()
            .Where(c => c.IsSystem)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;

        foreach (var (slug, name, description, displayOrder, requiresAdminForNewThreads) in SystemCategories)
        {
            var category = existing.FirstOrDefault(c => c.Slug == slug);

            if (category == null)
            {
                db.Set<ForumCategory>().Add(new ForumCategory
                {
                    Name = name,
                    Slug = slug,
                    Description = description,
                    DisplayOrder = displayOrder,
                    IsSystem = true,
                    RequiresAdminForNewThreads = requiresAdminForNewThreads,
                    CreatedAt = now,
                });
            }
            else
            {
                if (category.Name != name) category.Name = name;
                if (category.Description != description) category.Description = description;
                if (category.DisplayOrder != displayOrder) category.DisplayOrder = displayOrder;
                if (category.RequiresAdminForNewThreads != requiresAdminForNewThreads)
                    category.RequiresAdminForNewThreads = requiresAdminForNewThreads;
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another instance may have seeded concurrently — safe to ignore
        }
    }
}
