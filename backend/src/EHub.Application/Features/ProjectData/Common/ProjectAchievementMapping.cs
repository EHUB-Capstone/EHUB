using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;

namespace EHub.Application.Features.ProjectData.Common;

internal static class ProjectAchievementMapping
{
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
