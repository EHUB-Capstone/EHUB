using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Classes.Common;

public static class ClassSlugRules
{
    public const int MaxLength = 160;

    private static readonly Regex UnsafeCharacters = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex RepeatedHyphens = new("-{2,}", RegexOptions.Compiled);
    private static readonly Regex ValidSlug = new("^[a-z0-9](?:[a-z0-9-]{0,158}[a-z0-9])?$", RegexOptions.Compiled);

    public static string BuildBaseSlug(string semesterCode, string courseCode, int classIndex)
    {
        var parts = new[]
        {
            NormalizeSegment(semesterCode),
            NormalizeSegment(courseCode),
            NormalizeSegment(classIndex.ToString(CultureInfo.InvariantCulture))
        };

        return Truncate(string.Join('-', parts.Where(part => part.Length > 0)), MaxLength);
    }

    public static string MakeUnique(string baseSlug, IEnumerable<string> existingSlugs)
    {
        var normalizedBase = NormalizeSlugValue(baseSlug);
        if (normalizedBase.Length == 0)
        {
            normalizedBase = "class";
        }

        var used = existingSlugs
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => slug.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!used.Contains(normalizedBase))
        {
            return normalizedBase;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix += 1)
        {
            var suffixText = $"-{suffix}";
            var candidateBase = Truncate(normalizedBase, MaxLength - suffixText.Length);
            var candidate = $"{candidateBase}{suffixText}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique class slug.");
    }

    public static bool TryNormalizeRouteSlug(string? value, out string slug)
    {
        slug = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength || !ValidSlug.IsMatch(normalized))
        {
            return false;
        }

        slug = normalized;
        return true;
    }

    public static string NormalizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutDiacritics = RemoveDiacritics(value.Trim())
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .ToLowerInvariant();
        var slug = UnsafeCharacters.Replace(withoutDiacritics, "-");
        slug = RepeatedHyphens.Replace(slug, "-");
        return slug.Trim('-');
    }

    private static string NormalizeSlugValue(string value)
    {
        var slug = NormalizeSegment(value);
        return Truncate(slug, MaxLength);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength].Trim('-');
    }
}
