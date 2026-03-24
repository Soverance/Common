using System.Text.RegularExpressions;

namespace Soverance.Forum.Services;

public static partial class SlugGenerator
{
    public static string Generate(string input)
    {
        var slug = input.ToLowerInvariant().Trim();
        slug = NonAlphanumericRegex().Replace(slug, "-");
        slug = MultipleHyphenRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        return slug.Length == 0 ? "untitled" : slug;
    }

    public static string AppendSuffix(string slug, int suffix)
    {
        return $"{slug}-{suffix}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphenRegex();
}
