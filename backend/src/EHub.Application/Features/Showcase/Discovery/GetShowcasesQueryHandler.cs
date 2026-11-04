using EHub.Application.Common.Interfaces.Persistence;
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
    IValidator<GetShowcasesRequest> validator) : IGetShowcasesQueryHandler
{
    public async Task<Result<PagedResponse<ShowcaseCardDto>>> HandleAsync(
        GetShowcasesRequest request, CancellationToken cancellationToken = default)
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
        if (!string.IsNullOrWhiteSpace(request.Stage))
        {
            var stage = Enum.Parse<ShowcaseDevelopmentStage>(request.Stage.Trim(), true);
            query = query.Where(row => row.Showcase.DevelopmentStage == stage);
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = string.Equals(request.Sort.Trim(), "popular", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(row => row.Showcase.LikeCount).ThenByDescending(row => row.Showcase.PublishedAt).ThenBy(row => row.Showcase.Id)
            : query.OrderByDescending(row => row.Showcase.PublishedAt).ThenBy(row => row.Showcase.Id);
        var items = await ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(row => new ShowcaseCardDto
            {
                Id = row.Showcase.Id,
                ProjectName = row.Project.Name,
                Description = row.Project.Description ?? string.Empty,
                LogoUrl = row.Showcase.LogoUrl,
                DevelopmentStage = row.Showcase.DevelopmentStage.ToString(),
                PublishedAt = row.Showcase.PublishedAt,
                TeamName = row.Project.Team.TeamName,
                Industries = row.Project.ProjectTags.Where(tag => tag.TagType == ProjectTagType.StartupField)
                    .OrderBy(tag => tag.TagName).Select(tag => tag.TagName).ToArray(),
                LikeCount = row.Showcase.LikeCount,
                CommentCount = row.Showcase.CommentCount
            }).ToListAsync(cancellationToken);

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
