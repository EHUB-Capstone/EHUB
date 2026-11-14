using EHub.Domain.Entities;

namespace EHub.Application.Features.Mentoring.ExtractCv;

/// <summary>Adds source-backed professional facts while retaining manually reviewed profile values.</summary>
internal static class MentorCvProfileEnrichment
{
    public static void Apply(MentorProfile profile, MentorCvProfileData extracted)
    {
        profile.Bio = MergeText(profile.Bio, extracted.Bio, 2000);
        profile.Experience = MergeText(profile.Experience, extracted.Experience, 4000);
        profile.Expertise = MergeTags(profile.Expertise, extracted.Expertise);
        profile.StartupDomains = MergeTags(profile.StartupDomains, extracted.StartupDomains);
        profile.TechnologySkills = MergeTags(profile.TechnologySkills, extracted.TechnologySkills);

        foreach (var evidence in extracted.Evidence.Where(item => item.Field is "startupDomain" or "technology"))
        {
            var kind = evidence.Field == "startupDomain" ? "Startup" : "Technology";
            var retained = evidence.Field == "startupDomain" ? profile.StartupDomains : profile.TechnologySkills;
            if (profile.Experiences.Count >= 40 || !retained.Contains(evidence.Value, StringComparer.OrdinalIgnoreCase) ||
                profile.Experiences.Any(item => item.Kind == kind && item.Area.Equals(evidence.Value, StringComparison.OrdinalIgnoreCase)))
                continue;
            // This records a fact from an imported CV; retained rows need not belong to the latest uploaded file.
            profile.Experiences.Add(new MentorExperience
            {
                MentorProfileId = profile.Id, Kind = kind, Area = evidence.Value,
                Notes = "CV: " + evidence.Quote
            });
        }
    }

    private static string[] MergeTags(string[] existing, string[] extracted) =>
        existing.Concat(extracted).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();

    private static string? MergeText(string? existing, string? extracted, int maximum)
    {
        if (string.IsNullOrWhiteSpace(extracted)) return existing;
        if (string.IsNullOrWhiteSpace(existing)) return extracted;
        var merged = existing;
        foreach (var line in extracted.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (merged.Contains(line, StringComparison.Ordinal)) continue;
            // Never truncate an existing manual value or append a partial source sentence.
            if (merged.Length + 1 + line.Length <= maximum) merged += "\n" + line;
        }
        return merged;
    }
}
