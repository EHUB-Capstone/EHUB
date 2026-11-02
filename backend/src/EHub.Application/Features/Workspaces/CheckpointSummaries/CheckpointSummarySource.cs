using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

internal sealed record SummarySourceFile(Guid Id, int VersionNumber, string OriginalName, long FileSize, DateTime UploadedAt,
    Domain.Enums.SubmissionStorageProvider StorageProvider, string? StorageKey, string FileUrl);

internal sealed record SummarySourceLink(Guid Id, int VersionNumber, string Name, string Url);

internal sealed record SummarySourceRequirement(int Index, string Title);

/// <summary>What the checkpoint screen currently shows for a team: every visible file and link, newest first.</summary>
internal sealed record SummarySource(
    IReadOnlyList<SummarySourceFile> Files,
    IReadOnlyList<SummarySourceLink> Links,
    IReadOnlyList<SummarySourceRequirement> Requirements);

internal static class CheckpointSummarySource
{
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
        return new SummarySource(files, links, Requirements(checkpoint));
    }

    public static IReadOnlyList<SummarySourceRequirement> Requirements(Checkpoint checkpoint)
    {
        string[] titles;
        try { titles = JsonSerializer.Deserialize<string[]>(checkpoint.RequirementsJson) ?? []; }
        catch (JsonException) { titles = []; }
        return titles.Select((title, index) => new SummarySourceRequirement(index, title)).ToArray();
    }
}
