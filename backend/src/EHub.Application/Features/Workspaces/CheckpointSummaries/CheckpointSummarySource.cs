using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

internal sealed record SummarySourceFile(Guid Id, int VersionNumber, string OriginalName, long FileSize,
    Domain.Enums.SubmissionStorageProvider StorageProvider, string? StorageKey, string FileUrl);

internal sealed record SummarySourceLink(Guid Id, int VersionNumber, string Name, string Url);

internal sealed record SummarySourceRequirement(int Index, string Title, string? TeamAnswer);

/// <summary>What the checkpoint screen currently shows for a team: every visible file and link, newest first.</summary>
internal sealed record SummarySource(
    IReadOnlyList<SummarySourceFile> Files,
    IReadOnlyList<SummarySourceLink> Links,
    IReadOnlyList<SummarySourceRequirement> Requirements,
    string Hash)
{
    public bool IsEmpty => Files.Count == 0 && Links.Count == 0 && Requirements.All(item => string.IsNullOrWhiteSpace(item.TeamAnswer));
}

internal static class CheckpointSummarySource
{
    public static async Task<SummarySource> LoadAsync(
        IApplicationDbContext context, Guid teamId, Checkpoint checkpoint, CancellationToken cancellationToken)
    {
        // Each upload is a new Submission version and the screen lists the files and links of all of them.
        var files = await context.SubmissionFiles.AsNoTracking()
            .Where(file => file.Submission.TeamId == teamId && file.Submission.CheckpointId == checkpoint.Id)
            .OrderByDescending(file => file.VersionNumber).ThenByDescending(file => file.UploadedAt)
            .Select(file => new SummarySourceFile(file.Id, file.VersionNumber, file.OriginalName, file.FileSize,
                file.StorageProvider, file.StorageKey, file.FileUrl))
            .ToListAsync(cancellationToken);
        var links = await context.SubmissionLinks.AsNoTracking()
            .Where(link => link.Submission.TeamId == teamId && link.Submission.CheckpointId == checkpoint.Id)
            .OrderByDescending(link => link.VersionNumber).ThenByDescending(link => link.SubmittedAt)
            .Select(link => new SummarySourceLink(link.Id, link.VersionNumber, link.Name, link.Url))
            .ToListAsync(cancellationToken);
        // Requirement answers come from the latest submission, as on the checkpoint screen.
        var latestSubmissionId = await context.Submissions.AsNoTracking()
            .Where(submission => submission.TeamId == teamId && submission.CheckpointId == checkpoint.Id)
            .OrderByDescending(submission => submission.VersionNumber).ThenByDescending(submission => submission.CreatedAt)
            .Select(submission => (Guid?)submission.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var answers = latestSubmissionId is null
            ? new Dictionary<int, string>()
            : await context.SubmissionRequirementContents.AsNoTracking()
                .Where(content => content.SubmissionId == latestSubmissionId)
                .ToDictionaryAsync(content => content.RequirementIndex, content => content.Content, cancellationToken);
        var requirements = ReadRequirements(checkpoint.RequirementsJson)
            .Select((title, index) => new SummarySourceRequirement(index, title, answers.GetValueOrDefault(index)))
            .ToArray();
        return new SummarySource(files, links, requirements, Hash(files, links, requirements));
    }

    private static string[] ReadRequirements(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string Hash(
        IEnumerable<SummarySourceFile> files, IEnumerable<SummarySourceLink> links, IEnumerable<SummarySourceRequirement> requirements)
    {
        var builder = new StringBuilder();
        foreach (var file in files) builder.Append("f|").Append(file.Id).Append('|').Append(file.VersionNumber).Append('\n');
        foreach (var link in links) builder.Append("l|").Append(link.Id).Append('|').Append(link.VersionNumber).Append('|').Append(link.Url).Append('\n');
        foreach (var requirement in requirements)
            builder.Append("r|").Append(requirement.Index).Append('|').Append(requirement.Title).Append('|').Append(requirement.TeamAnswer).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
