using System.Text.RegularExpressions;

namespace EHub.Application.Features.Admin.MentorProfiles;

// One set of rules for mentor profile text, shared by the profile form and the create-user form.
internal static partial class MentorProfileRules
{
    public const int MaximumExpertiseItems = 20;
    public const int MinimumExpertiseLength = 2;
    public const int MaximumExpertiseLength = 50;
    public const int MaximumBioLength = 2000;
    public const int MaximumAvailabilityNoteLength = 500;

    // Trims, collapses inner spaces, drops empty entries and rejects duplicates (ignoring case) and out-of-range tags.
    public static (string[] Values, string? Error) NormalizeExpertise(IEnumerable<string>? raw)
    {
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in raw ?? [])
        {
            var value = Spaces().Replace(item?.Trim() ?? string.Empty, " ");
            if (value.Length == 0) continue;
            if (value.Length < MinimumExpertiseLength || value.Length > MaximumExpertiseLength)
                return ([], $"Each expertise must be {MinimumExpertiseLength} to {MaximumExpertiseLength} characters: '{Shorten(value)}'.");
            if (!seen.Add(value)) return ([], $"Expertise '{value}' is listed more than once.");
            values.Add(value);
        }
        return values.Count > MaximumExpertiseItems
            ? ([], $"A mentor can have at most {MaximumExpertiseItems} expertise tags.")
            : (values.ToArray(), null);
    }

    public static string? ValidateBio(string? bio) =>
        (bio?.Trim().Length ?? 0) > MaximumBioLength ? $"Background may contain at most {MaximumBioLength} characters." : null;

    public static string? ValidateAvailabilityNote(string? note) =>
        (note?.Trim().Length ?? 0) > MaximumAvailabilityNoteLength ? $"Availability note may contain at most {MaximumAvailabilityNoteLength} characters." : null;

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Shorten(string value) => value.Length <= 20 ? value : value[..20] + "...";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
