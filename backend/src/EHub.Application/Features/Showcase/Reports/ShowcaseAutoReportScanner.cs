using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Reports;

public interface IShowcaseAutoReportScanner
{
    Task ScanPageAsync(Guid showcaseId, CancellationToken cancellationToken = default);
    Task ScanCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
}

public sealed class ShowcaseAutoReportScanner(IApplicationDbContext context, IDateTimeProvider time) : IShowcaseAutoReportScanner
{
    public async Task ScanPageAsync(Guid showcaseId, CancellationToken cancellationToken = default)
    {
        var showcase = await context.ShowcaseProjects.Include(x => x.Links)
            .SingleOrDefaultAsync(x => x.Id == showcaseId && (x.Status == ShowcaseStatus.Published || x.Status == ShowcaseStatus.PendingReview), cancellationToken);
        if (showcase is null) return;
        var project = await ShowcaseAccess.LatestProjectAsync(context, showcase.ProjectLineageId, cancellationToken);
        if (project is null) return;
        var fields = new[] { project.ProjectName, project.Description, project.Problem ?? "", project.Solution ?? "", project.TargetUsers ?? "", showcase.Highlights ?? "" }
            .Concat(showcase.Links.SelectMany(x => new[] { x.Label, x.Url })).ToArray();
        await ScanAsync(showcase, null, ShowcaseContentFilter.PageText(project, showcase), fields, project.LecturerUserIds, cancellationToken);
    }

    public async Task ScanCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var comment = await context.ShowcaseComments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == commentId, cancellationToken);
        if (comment is null) return;
        var showcase = await context.ShowcaseProjects.SingleOrDefaultAsync(x => x.Id == comment.ShowcaseProjectId && x.Status == ShowcaseStatus.Published, cancellationToken);
        if (showcase is null) return;
        var project = await ShowcaseAccess.LatestProjectAsync(context, showcase.ProjectLineageId, cancellationToken);
        if (project is null) return;
        await ScanAsync(showcase, comment.Id, comment.Content, [comment.Content], project.LecturerUserIds, cancellationToken);
    }

    private async Task ScanAsync(ShowcaseProject showcase, Guid? commentId, string text, string[] fields, IReadOnlyCollection<Guid> reviewers, CancellationToken token)
    {
        var rules = await context.ShowcaseModerationRules.AsNoTracking().Where(x => x.IsEnabled).OrderBy(x => x.Id).ToListAsync(token);
        var contentHash = ShowcaseContentFilter.Hash(text);
        var fingerprint = ShowcaseContentFilter.Hash(contentHash + JsonSerializer.Serialize(rules.Select(x => new { x.Id, x.Phrase, x.Reason, x.Description })) + "filter-v2");
        var scan = await context.ShowcaseModerationScans.SingleOrDefaultAsync(x => x.ShowcaseProjectId == showcase.Id && x.CommentId == commentId, token);
        if (scan?.Fingerprint == fingerprint) return;

        var findings = ShowcaseContentFilter.Evaluate(fields, rules, commentId.HasValue);
        var openSystemReports = await context.ShowcaseReports.Where(x => x.ShowcaseProjectId == showcase.Id && x.CommentId == commentId
            && x.Source == ShowcaseReportSource.System && x.Status == ShowcaseReportStatus.Open).ToListAsync(token);
        // A changed item must be assessed on its current content, not on an old flag.
        foreach (var old in openSystemReports.Where(x => x.DetectionKey != fingerprint || findings.Count == 0))
        {
            old.Status = ShowcaseReportStatus.Dismissed;
            old.ResolvedAt = time.UtcNow;
            old.ResolutionNote = "Superseded by a content/rule update / Nội dung hoặc quy tắc đã thay đổi.";
        }
        var existing = await context.ShowcaseReports.AsNoTracking().AnyAsync(x => x.ShowcaseProjectId == showcase.Id && x.CommentId == commentId
            && x.Source == ShowcaseReportSource.System && x.DetectionKey == fingerprint, token);
        if (findings.Count > 0 && !existing && !openSystemReports.Any(x => x.Status == ShowcaseReportStatus.Open))
        {
            var note = string.Join("\n", findings.Select(x => x.Evidence));
            context.ShowcaseReports.Add(new ShowcaseReport
            {
                ShowcaseProjectId = showcase.Id, CommentId = commentId, Source = ShowcaseReportSource.System,
                ContentFingerprint = contentHash, DetectionKey = fingerprint,
                Reason = findings[0].Reason,
                Note = note[..Math.Min(1000, note.Length)],
                CreatedAt = time.UtcNow
            });
            ShowcaseOutbox.EnqueueNotification(context, showcase.Id, reviewers, "Automatic content flag",
                "Content matched a review rule / Nội dung khớp quy tắc và cần kiểm tra.", "/showcase/moderation?tab=reports", time.UtcNow, notifyAdministrators: true);
        }
        if (scan is null)
        {
            scan = new ShowcaseModerationScan { ShowcaseProjectId = showcase.Id, CommentId = commentId };
            context.ShowcaseModerationScans.Add(scan);
        }
        scan.Fingerprint = fingerprint;
        scan.ScannedAt = time.UtcNow;
        try
        {
            // Report, scan marker, superseded flags and notification commit together. The unique indexes and
            // xmin token also protect concurrent scans by multiple API instances.
            await context.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            context.ClearChanges();
            // Suppress only a scan another worker actually committed; all other DB errors remain visible.
            if (!await context.ShowcaseModerationScans.AsNoTracking().AnyAsync(x => x.ShowcaseProjectId == showcase.Id && x.CommentId == commentId && x.Fingerprint == fingerprint, token)) throw;
        }
    }
}
