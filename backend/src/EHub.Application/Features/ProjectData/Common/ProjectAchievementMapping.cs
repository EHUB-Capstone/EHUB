using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;

namespace EHub.Application.Features.ProjectData.Common;

internal static class ProjectAchievementMapping
{
    internal const int NoteMaxLength = 500;

    // Activity-log actions that make up a project's achievement history.
    internal const string ChangedAction = "PROJECT_ACHIEVEMENTS_CHANGED";
    internal const string CarriedOverAction = "ACHIEVEMENTS_CARRIED_OVER";

    /// <summary>Trims the note and treats blank as no note.</summary>
    internal static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    internal static IReadOnlyCollection<string> ToNames(bool isHighPotential, bool isFunded, bool isAwarded)
    {
        var names = new List<string>(3);
        if (isHighPotential) names.Add(ProjectAchievementNames.Potential);
        if (isFunded) names.Add(ProjectAchievementNames.Funded);
        if (isAwarded) names.Add(ProjectAchievementNames.Awarded);
        return names;
    }

    internal static IReadOnlyCollection<string> ToNames(Project project) =>
        ToNames(project.IsHighPotential, project.IsFunded, project.IsAwarded);

    /// <summary>Maps a client-supplied label to its canonical spelling, or null when it is not a known label.</summary>
    internal static string? Canonicalize(string? value) =>
        ProjectAchievementNames.All.FirstOrDefault(name =>
            string.Equals(name, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
