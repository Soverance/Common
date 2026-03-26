using Microsoft.EntityFrameworkCore;
using Soverance.Forum.Models;

namespace Soverance.Forum.Services;

public static class ForumSeeder
{
    private static readonly (string Slug, string Name, string Description, int DisplayOrder)[] SystemCategories =
    [
        ("news", "News", "News updates about Vanalytics", 0),
        ("help", "Help", "Help with features in Vanalytics", 1),
        ("bugs", "Bugs", "Report bugs in Vanalytics", 2),
        ("suggestions", "Suggestions", "Request new features for Vanalytics", 3),
    ];

    public static async Task SeedSystemCategoriesAsync(DbContext db)
    {
        var existing = await db.Set<ForumCategory>()
            .Where(c => c.IsSystem)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;

        foreach (var (slug, name, description, displayOrder) in SystemCategories)
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
                    CreatedAt = now,
                });
            }
            else
            {
                if (category.Name != name) category.Name = name;
                if (category.Description != description) category.Description = description;
                if (category.DisplayOrder != displayOrder) category.DisplayOrder = displayOrder;
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
