using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Featured;

public sealed class ShowcaseFeaturedHandler(IApplicationDbContext context, IDateTimeProvider time) : IShowcaseFeaturedHandler
{
    public async Task<Result<ShowcaseFeaturedDto>> SetAsync(
        Guid showcaseId, bool featured, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role))
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseAccessDenied, "Only lecturers and administrators can select featured showcases.");
        // Resolve review scope before inspecting the state, so foreign ids always look missing.
        var showcase = await ShowcaseAccess.ModeratableShowcases(context, userId, role)
            .FirstOrDefaultAsync(item => item.Id == showcaseId, cancellationToken);
        if (showcase is null)
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");
        if (showcase.Status != ShowcaseStatus.Published)
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseInvalidState, "Only published showcases can be featured or have their featured flag removed.");
        // Retried requests do not change the original timestamp or create duplicate history.
        if (showcase.IsFeatured == featured) return Result.Success(ToDto(showcase));

        var now = time.UtcNow;
        // PostgreSQL stores microseconds. Match that precision before saving so the first response
        // and an idempotent retry report exactly the same stored selection timestamp.
        now = now.AddTicks(-(now.Ticks % 10));
        showcase.IsFeatured = featured;
        showcase.FeaturedAt = featured ? now : null;
        showcase.UpdatedBy = userId;
        context.ShowcaseReviewHistories.Add(new ShowcaseReviewHistory
        {
            ShowcaseProjectId = showcase.Id,
            FromStatus = showcase.Status, ToStatus = showcase.Status,
            Note = featured ? "Selected as a featured showcase by faculty." : "Featured selection removed by faculty.",
            ActorId = userId, CreatedAt = now
        });
        try
        {
            // Flag and audit history are atomic; existing xmin handles concurrent edits/hides.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ClearChanges();
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseConcurrencyConflict, "The showcase page was changed by someone else. Reload and try again.");
        }
        return Result.Success(ToDto(showcase));
    }

    public const int MaximumBulkIds = 500;

    public async Task<Result<BulkFeatureShowcasesResultDto>> SetManyAsync(
        IReadOnlyCollection<Guid> showcaseIds, bool featured, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role))
            return Result.Failure<BulkFeatureShowcasesResultDto>(ErrorCodes.ShowcaseAccessDenied, "Only lecturers and administrators can select featured showcases.");
        var ids = showcaseIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result.Failure<BulkFeatureShowcasesResultDto>(ErrorCodes.ShowcaseValidationError, "Select at least one showcase.");
        if (ids.Count > MaximumBulkIds)
            return Result.Failure<BulkFeatureShowcasesResultDto>(ErrorCodes.ShowcaseValidationError, $"Select at most {MaximumBulkIds} showcases at a time.");

        // Scope is resolved by the query, so ids outside the caller's classes simply do not load.
        var showcases = await ShowcaseAccess.ModeratableShowcases(context, userId, role)
            .Where(item => ids.Contains(item.Id) && item.Status == ShowcaseStatus.Published)
            .ToListAsync(cancellationToken);
        var changing = showcases.Where(item => item.IsFeatured != featured).ToList();

        var now = time.UtcNow;
        now = now.AddTicks(-(now.Ticks % 10));
        foreach (var showcase in changing)
        {
            showcase.IsFeatured = featured;
            showcase.FeaturedAt = featured ? now : null;
            showcase.UpdatedBy = userId;
            context.ShowcaseReviewHistories.Add(new ShowcaseReviewHistory
            {
                ShowcaseProjectId = showcase.Id,
                FromStatus = showcase.Status, ToStatus = showcase.Status,
                Note = featured ? "Selected as a featured showcase by faculty." : "Featured selection removed by faculty.",
                ActorId = userId, CreatedAt = now
            });
        }
        if (changing.Count > 0)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                context.ClearChanges();
                return Result.Failure<BulkFeatureShowcasesResultDto>(ErrorCodes.ShowcaseConcurrencyConflict, "A showcase page was changed by someone else. Reload and try again.");
            }
        }
        return Result.Success(new BulkFeatureShowcasesResultDto
        {
            Updated = changing.Count,
            Unchanged = showcases.Count - changing.Count,
            Skipped = ids.Count - showcases.Count
        });
    }

    public async Task<Result<IReadOnlyList<FeaturedCandidateDto>>> ListCandidatesAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role))
            return Result.Failure<IReadOnlyList<FeaturedCandidateDto>>(ErrorCodes.ShowcaseAccessDenied, "Only lecturers and administrators can select featured showcases.");

        var showcases = await ShowcaseAccess.ModeratableShowcases(context, userId, role).AsNoTracking()
            .Where(item => item.Status == ShowcaseStatus.Published)
            .Select(item => new { item.Id, item.ProjectLineageId, item.LogoUrl, item.DevelopmentStage, item.IsFeatured })
            .ToListAsync(cancellationToken);
        var lineageIds = showcases.Select(item => item.ProjectLineageId).Distinct().ToList();
        var projects = await context.Projects.AsNoTracking()
            .Where(project => lineageIds.Contains(project.ProjectLineageId))
            .Select(project => new
            {
                project.ProjectLineageId,
                project.CreatedAt,
                project.Name,
                project.Team.TeamName,
                project.Team.Class.ClassCode,
                LecturerName = project.Team.Class.PrimaryLecturer != null
                    ? project.Team.Class.PrimaryLecturer.FullName
                    : project.Team.Class.ClassLecturers.OrderBy(assignment => assignment.AssignedAt)
                        .Select(assignment => assignment.Lecturer.FullName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var latest = projects.GroupBy(project => project.ProjectLineageId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(project => project.CreatedAt).First());

        IReadOnlyList<FeaturedCandidateDto> items = showcases
            .Where(item => latest.ContainsKey(item.ProjectLineageId))
            .Select(item =>
            {
                var project = latest[item.ProjectLineageId];
                return new FeaturedCandidateDto
                {
                    Id = item.Id,
                    ProjectName = project.Name,
                    LogoUrl = item.LogoUrl,
                    DevelopmentStage = item.DevelopmentStage.ToString(),
                    TeamName = project.TeamName,
                    ClassCode = project.ClassCode,
                    LecturerName = project.LecturerName ?? string.Empty,
                    IsFeatured = item.IsFeatured
                };
            })
            .OrderBy(item => item.ClassCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Result.Success(items);
    }

    private static ShowcaseFeaturedDto ToDto(ShowcaseProject showcase) => new()
    {
        Id = showcase.Id, IsFeatured = showcase.IsFeatured, FeaturedAt = showcase.FeaturedAt, RowVersion = showcase.Version
    };
}
