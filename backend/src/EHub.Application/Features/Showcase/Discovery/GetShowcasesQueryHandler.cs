using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using EHub.Domain.Entities;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Discovery;

public sealed class GetShowcasesQueryHandler(
    IApplicationDbContext context,
    IShowcaseProjectSearch search,
    IValidator<GetShowcasesRequest> validator,
    IDateTimeProvider time) : IGetShowcasesQueryHandler
{
    public async Task<Result<PagedResponse<ShowcaseCardDto>>> HandleAsync(
        GetShowcasesRequest request, Guid viewerId = default, string viewerRole = "", CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<PagedResponse<ShowcaseCardDto>>(ErrorCodes.ShowcaseValidationError, validation.Errors[0].ErrorMessage);

        // Select the latest profile BEFORE filtering, so an older matching profile cannot reappear.
        // Id breaks equal creation timestamps deterministically, yielding one card per lineage.
        var projects = LatestProjects();

        if (!string.IsNullOrWhiteSpace(request.Search)) projects = search.Apply(projects, request.Search.Trim());
        if (!string.IsNullOrWhiteSpace(request.Industry))
        {
            var industry = request.Industry.Trim().ToUpperInvariant();
            projects = projects.Where(project => project.ProjectTags.Any(tag =>
                tag.TagType == ProjectTagType.StartupField && tag.NormalizedTagName == industry));
        }
        if (request.SemesterId is { } semesterId)
            projects = projects.Where(project => project.Team.Class.SemesterId == semesterId);

        var query = from showcase in context.ShowcaseProjects.AsNoTracking()
                    join project in projects on showcase.ProjectLineageId equals project.ProjectLineageId
                    where showcase.Status == ShowcaseStatus.Published
                    select new { Showcase = showcase, Project = project };
        if (request.Featured is { } featured)
            query = query.Where(row => row.Showcase.IsFeatured == featured);
        if (!string.IsNullOrWhiteSpace(request.Stage))
        {
            var stage = Enum.Parse<ShowcaseDevelopmentStage>(request.Stage.Trim(), true);
            query = query.Where(row => row.Showcase.DevelopmentStage == stage);
        }

        var now = time.UtcNow;
        var period = request.Period?.Trim();
        if (string.Equals(period, "topThisWeek", StringComparison.OrdinalIgnoreCase))
        {
            // Keep the launch rail useful during a quiet week without treating older pages as new launches.
            // The fourteen-day fallback is intentionally used only when the normal seven-day window is empty.
            var sevenDaysAgo = now.AddDays(-7);
            var recent = query.Where(row => row.Showcase.PublishedAt >= sevenDaysAgo && row.Showcase.PublishedAt <= now);
            query = await recent.AnyAsync(cancellationToken)
                ? recent
                : query.Where(row => row.Showcase.PublishedAt >= now.AddDays(-14) && row.Showcase.PublishedAt <= now);
        }
        else if (!string.IsNullOrWhiteSpace(period))
        {
            var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            var lastWeek = string.Equals(period, "lastWeek", StringComparison.OrdinalIgnoreCase);
            var start = lastWeek ? monday.AddDays(-7) : monday;
            var end = lastWeek ? monday : monday.AddDays(7);
            query = query.Where(row => row.Showcase.PublishedAt >= start && row.Showcase.PublishedAt < end
                && row.Showcase.PublishedAt <= now);
        }
        var total = await query.CountAsync(cancellationToken);
        var sort = string.Equals(period, "topThisWeek", StringComparison.OrdinalIgnoreCase) ? "trending" : request.Sort.Trim();
        var ordered = query.OrderByDescending(row => row.Showcase.PublishedAt);
        if (string.Equals(sort, "trending", StringComparison.OrdinalIgnoreCase))
        {
            var todayUtc = DateOnly.FromDateTime(now);
            var oneDayAgo = now.AddDays(-1);
            var twoDaysAgo = now.AddDays(-2);
            var threeDaysAgo = now.AddDays(-3);
            var fourDaysAgo = now.AddDays(-4);
            var fiveDaysAgo = now.AddDays(-5);

            // Use timestamp comparisons so PostgreSQL can rank the entire filtered set before paging.
            ordered = query.OrderByDescending(row => (long)row.Showcase.LikeCount
                + 3L * context.ShowcaseUpvotes.LongCount(vote =>
                    vote.ShowcaseProjectId == row.Showcase.Id && vote.CreatedAt >= oneDayAgo && vote.CreatedAt <= now)
                + 0.5 * Math.Sqrt(context.ShowcaseViewsDaily.LongCount(view =>
                    view.ShowcaseProjectId == row.Showcase.Id && view.ViewDateUtc == todayUtc))
                + (row.Showcase.PublishedAt > now ? 0
                    : row.Showcase.PublishedAt > oneDayAgo ? 5
                    : row.Showcase.PublishedAt > twoDaysAgo ? 4
                    : row.Showcase.PublishedAt > threeDaysAgo ? 3
                    : row.Showcase.PublishedAt > fourDaysAgo ? 2
                    : row.Showcase.PublishedAt > fiveDaysAgo ? 1
                    : 0))
                .ThenByDescending(row => row.Showcase.PublishedAt.HasValue)
                .ThenByDescending(row => row.Showcase.PublishedAt);
        }
        else if (string.Equals(sort, "popular", StringComparison.OrdinalIgnoreCase))
        {
            ordered = query.OrderByDescending(row => row.Showcase.LikeCount).ThenByDescending(row => row.Showcase.PublishedAt);
        }

        var ownedProjects = ShowcaseAccess.OwnedProjects(context, viewerId);
        var viewerIsStudent = ShowcaseAccess.IsStudent(viewerRole);
        var items = await ordered.ThenBy(row => row.Showcase.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(row => new ShowcaseCardDto
            {
                Id = row.Showcase.Id,
                ProjectName = row.Project.Name,
                Description = row.Project.Description ?? string.Empty,
                LogoUrl = row.Showcase.LogoUrl,
                DevelopmentStage = row.Showcase.DevelopmentStage.ToString(),
                PublishedAt = row.Showcase.PublishedAt,
                IsFeatured = row.Showcase.IsFeatured,
                FeaturedAt = row.Showcase.FeaturedAt,
                TeamName = row.Project.Team.TeamName,
                Industries = row.Project.ProjectTags.Where(tag => tag.TagType == ProjectTagType.StartupField)
                    .OrderBy(tag => tag.TagName).Select(tag => tag.TagName).ToArray(),
                LikeCount = row.Showcase.LikeCount,
                ViewCount = row.Showcase.ViewCount,
                HasUpvoted = context.ShowcaseUpvotes.Any(vote => vote.ShowcaseProjectId == row.Showcase.Id && vote.UserId == viewerId),
                IsOwner = viewerIsStudent && ownedProjects.Any(project => project.ProjectLineageId == row.Showcase.ProjectLineageId),
                CommentCount = row.Showcase.CommentCount,
                CoverUrl = row.Showcase.Media.Where(media => media.MediaType == ShowcaseMediaType.Image)
                    .OrderBy(media => media.SortOrder).ThenBy(media => media.Id).Select(media => media.Url).FirstOrDefault(),
                VideoUrl = row.Showcase.Links.Where(link => link.LinkType == ShowcaseLinkType.Video)
                    .OrderBy(link => link.SortOrder).ThenBy(link => link.Id).Select(link => link.Url).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        // Links saved before the provider rule existed are dropped here instead of being sent to the feed.
        foreach (var item in items) item.VideoUrl = ShowcaseVideoLinks.Normalize(item.VideoUrl);

        return Result.Success(new PagedResponse<ShowcaseCardDto>
        {
            Items = items, PageIndex = request.Page, PageSize = request.PageSize,
            TotalItems = total, TotalPages = (int)Math.Ceiling(total / (double)request.PageSize)
        });
    }

    private IQueryable<Project> LatestProjects() => context.Projects.AsNoTracking().Where(project => project.Id == context.Projects
        .Where(other => other.ProjectLineageId == project.ProjectLineageId)
        .OrderByDescending(other => other.CreatedAt).ThenByDescending(other => other.Id)
        .Select(other => other.Id).First());

    public async Task<Result<ShowcaseFilterOptionsDto>> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        // Filter choices come from catalogs, independently of the current showcase results.
        // Match the existing startup-industry options endpoint: only active, non-deleted entries.
        var industries = await context.StartupIndustries.AsNoTracking()
            .Where(industry => industry.Status == StartupIndustryStatus.Active)
            .OrderBy(industry => industry.Name).Select(industry => industry.Name).ToListAsync(cancellationToken);
        var semesters = await context.Semesters.AsNoTracking()
            .OrderByDescending(semester => semester.Year)
            .ThenByDescending(semester => semester.Term == SemesterTerm.Fall ? 2 : semester.Term == SemesterTerm.Summer ? 1 : 0)
            .ThenBy(semester => semester.Id)
            .Select(semester => new ShowcaseSemesterOptionDto { Id = semester.Id, Code = semester.Code, Name = semester.Name })
            .ToListAsync(cancellationToken);
        return Result.Success(new ShowcaseFilterOptionsDto { Industries = industries, Semesters = semesters });
    }
}
