using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

internal sealed record SummarySourceFile(Guid Id, int VersionNumber, string OriginalName, long FileSize, DateTime UploadedAt,
    Domain.Enums.SubmissionStorageProvider StorageProvider, string? StorageKey, string FileUrl);

internal sealed record SummarySourceLink(Guid Id, int VersionNumber, string Name, string Url);

/// <summary>One thing reviewers look for: a checkpoint requirement or a rubric criterion.</summary>
/// <param name="Description">The criterion's description, or when it has none the Excellent level text of the imported rubric.</param>
/// <param name="Weight">Share of the checkpoint grade, for criteria.</param>
/// <param name="Assessment">Document, Product or Live: whether a submitted file can show it (<see cref="CheckpointCriterionProfile"/>).</param>
/// <param name="Excellent">The Excellent level text, from which the evidence cues are taken.</param>
internal sealed record ChecklistItem(
    int Index, string Kind, string Title, string? Description,
    decimal? Weight = null, string Assessment = CheckpointCriterionProfile.Document, string? Excellent = null);

/// <summary>What the checkpoint screen currently shows for a team: every visible file and link, newest first.</summary>
internal sealed record SummarySource(
    IReadOnlyList<SummarySourceFile> Files,
    IReadOnlyList<SummarySourceLink> Links,
    IReadOnlyList<ChecklistItem> Checklist);

internal static class CheckpointSummarySource
{
    public const string RequirementKind = "Requirement";
    public const string CriterionKind = "Criterion";

    public static async Task<SummarySource> LoadAsync(
        IApplicationDbContext context, Guid teamId, Checkpoint checkpoint, CancellationToken cancellationToken)
    {
        // Each upload is a new Submission version and the screen lists the files and links of all of them.
        var files = await context.SubmissionFiles.AsNoTracking()
            .Where(file => file.Submission.TeamId == teamId && file.Submission.CheckpointId == checkpoint.Id)
            .OrderByDescending(file => file.VersionNumber).ThenByDescending(file => file.UploadedAt)
            .Select(file => new SummarySourceFile(file.Id, file.VersionNumber, file.OriginalName, file.FileSize, file.UploadedAt,
                file.StorageProvider, file.StorageKey, file.FileUrl))
            .ToListAsync(cancellationToken);
        var links = await context.SubmissionLinks.AsNoTracking()
            .Where(link => link.Submission.TeamId == teamId && link.Submission.CheckpointId == checkpoint.Id)
            .OrderByDescending(link => link.VersionNumber).ThenByDescending(link => link.SubmittedAt)
            .Select(link => new SummarySourceLink(link.Id, link.VersionNumber, link.Name, link.Url))
            .ToListAsync(cancellationToken);
        return new SummarySource(files, links, await ChecklistAsync(context, checkpoint, cancellationToken));
    }

    /// <summary>
    /// The checkpoint's requirements followed by its rubric criteria. Many checkpoints define only a rubric, which is
    /// exactly what lecturers grade against, so both are checked.
    /// </summary>
    public static async Task<IReadOnlyList<ChecklistItem>> ChecklistAsync(
        IApplicationDbContext context, Checkpoint checkpoint, CancellationToken cancellationToken)
    {
        string[] requirements;
        try { requirements = JsonSerializer.Deserialize<string[]>(checkpoint.RequirementsJson) ?? []; }
        catch (JsonException) { requirements = []; }
        var criteria = await context.RubricCriteria.AsNoTracking()
            .Where(criterion => criterion.Rubric.CheckpointId == checkpoint.Id)
            .OrderBy(criterion => criterion.DisplayOrder)
            .Select(criterion => new { criterion.Name, criterion.Description, criterion.Weight, criterion.LevelsJson })
            .ToListAsync(cancellationToken);

        var items = requirements.Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => new ChecklistItem(0, RequirementKind, title.Trim(), null))
            .Concat(criteria.Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    // A rubric imported from a spreadsheet keeps its text in the four levels and leaves the description
                    // empty; the Excellent level says what a lecturer expects to see, so it stands in for it.
                    var excellent = CheckpointCriterionProfile.ExcellentText(first.LevelsJson);
                    return new ChecklistItem(0, CriterionKind, group.Key,
                        string.IsNullOrWhiteSpace(first.Description) ? excellent : first.Description,
                        first.Weight, CheckpointCriterionProfile.Classify(group.Key), excellent);
                }))
            .Take(20)
            .ToArray();
        return items.Select((item, index) => item with { Index = index }).ToArray();
    }
}
