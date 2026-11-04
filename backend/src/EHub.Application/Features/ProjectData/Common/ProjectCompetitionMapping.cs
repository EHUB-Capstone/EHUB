using System.Linq.Expressions;
using System.Text.RegularExpressions;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.ProjectData.Common;

internal static partial class ProjectCompetitionMapping
{
    internal const string ChangedAction = "PROJECT_COMPETITIONS_CHANGED";

    /// <summary>Maps a client-supplied name to its canonical spelling, or null when it is not a listed competition.</summary>
    internal static string? Canonicalize(string? value) =>
        ProjectCompetitionNames.All.FirstOrDefault(name =>
            string.Equals(name, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Accepts the stage names only, never a number that happens to match an enum value.</summary>
    internal static CompetitionStage? ParseStage(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || int.TryParse(text, out _)) return null;
        return Enum.TryParse<CompetitionStage>(text, ignoreCase: true, out var stage) && Enum.IsDefined(stage)
            ? stage
            : null;
    }

    internal static bool IsOthers(string competition) =>
        string.Equals(competition, ProjectCompetitionNames.Others, StringComparison.Ordinal);

    /// <summary>Trims and collapses inner whitespace so "  Tech   Cup " and "tech cup" are the same competition.</summary>
    internal static string? CleanOtherName(string? value)
    {
        var cleaned = Whitespace().Replace(value ?? string.Empty, " ").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    internal static string NormalizeOtherName(string? cleaned) => cleaned?.ToLowerInvariant() ?? string.Empty;

    internal static string DisplayName(string competition, string? otherName) =>
        IsOthers(competition) && !string.IsNullOrWhiteSpace(otherName) ? otherName : competition;

    internal static ProjectCompetitionResponse ToResponse(ProjectCompetitionEntry entry) => new()
    {
        Competition = entry.Competition,
        OtherName = entry.OtherName,
        Name = DisplayName(entry.Competition, entry.OtherName),
        Year = entry.Year,
        Stage = entry.Stage.ToString(),
    };

    /// <summary>Newest year first, then the order of the list, then the free-text name.</summary>
    internal static ProjectCompetitionResponse[] ToOrderedResponses(IEnumerable<ProjectCompetitionEntry> entries) =>
        entries
            .OrderByDescending(entry => entry.Year)
            .ThenBy(entry => IndexOf(entry.Competition))
            .ThenBy(entry => entry.NormalizedOtherName, StringComparer.Ordinal)
            .Select(ToResponse)
            .ToArray();

    internal static string Describe(ProjectCompetitionEntry entry) =>
        $"{DisplayName(entry.Competition, entry.OtherName)} {entry.Year} ({entry.Stage})";

    /// <summary>Position in the fixed list; names outside it (which cannot be stored) sort last.</summary>
    internal static int IndexOf(string competition)
    {
        for (var index = 0; index < ProjectCompetitionNames.All.Count; index++)
        {
            if (string.Equals(ProjectCompetitionNames.All[index], competition, StringComparison.Ordinal)) return index;
        }

        return int.MaxValue;
    }

    /// <summary>
    /// The entry-level part of the competition filters, or null when none is set. Used on the project list, and on the
    /// export, which then lists only the entries that match.
    /// </summary>
    internal static Expression<Func<ProjectCompetitionEntry, bool>>? EntryFilter(GetProjectDataRequest request)
    {
        var competitions = ProjectDataQuery.NormalizeAll(request.Competition, value => Canonicalize(value) ?? string.Empty)
            .Where(value => value.Length > 0)
            .ToArray();
        var stages = ProjectDataQuery.NormalizeAll(request.CompetitionStage, value => value)
            .Select(ParseStage)
            .OfType<CompetitionStage>()
            .Distinct()
            .ToArray();
        var years = request.CompetitionYear?.Distinct().ToArray() ?? [];

        if (competitions.Length == 0 && stages.Length == 0 && years.Length == 0) return null;

        return entry =>
            (competitions.Length == 0 || competitions.Contains(entry.Competition)) &&
            (stages.Length == 0 || stages.Contains(entry.Stage)) &&
            (years.Length == 0 || years.Contains(entry.Year));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
