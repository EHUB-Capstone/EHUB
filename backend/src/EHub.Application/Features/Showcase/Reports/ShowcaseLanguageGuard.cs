using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Reports;

public static class ShowcaseLanguageGuard
{
    public static async Task<Result> ValidateAsync(IApplicationDbContext context, IEnumerable<string?> fields, CancellationToken token)
    {
        var rules = await context.ShowcaseModerationRules.AsNoTracking()
            .Where(x => x.Status == ShowcaseModerationRuleStatus.Active && ShowcaseViolationCategories.PhraseBlockingCategories.Contains(x.Category))
            .ToListAsync(token);
        // Only explicit phrase rules block submissions. The old link-count signal never blocks a legitimate demo URL.
        var findings = ShowcaseContentFilter.Evaluate(fields.Select(x => x ?? ""), rules, isComment: false, allMatches: true);
        return findings.Count == 0
            ? Result.Success()
            : Result.Failure(ShowcaseViolationCategories.ErrorFor(findings.Select(x => x.Category)));
    }

    public static async Task<Result> ValidatePageAsync(IApplicationDbContext context, Guid lineageId,
        string? highlights, IEnumerable<ShowcaseLinkRequest> links, CancellationToken token)
    {
        var project = await ShowcaseAccess.LatestProjectAsync(context, lineageId, token);
        if (project is null) return Result.Failure(ErrorCodes.ShowcaseNotFound, "The project was not found.");
        return await ValidateAsync(context, new[] { project.ProjectName, project.Description, project.Problem, project.Solution, project.TargetUsers, highlights }
            .Concat(links.SelectMany(x => new[] { x.Label, x.Url })), token);
    }

    // This narrow check protects the profile text displayed by a showcase, without changing projects that have no showcase.
    public static async Task<Result> ValidateProfileIfShowcasedAsync(IApplicationDbContext context, Project project,
        IEnumerable<string?> fields, CancellationToken token)
    {
        if (!await context.ShowcaseProjects.AsNoTracking().AnyAsync(x => x.ProjectLineageId == project.ProjectLineageId, token)
            || await context.Projects.AsNoTracking().AnyAsync(x => x.ProjectLineageId == project.ProjectLineageId && x.CreatedAt > project.CreatedAt, token))
            return Result.Success();
        return await ValidateAsync(context, fields, token);
    }
}
