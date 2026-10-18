using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.ManageStartupIndustries;

public sealed class UpdateProjectStartupIndustriesCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IUpdateProjectStartupIndustriesCommandHandler
{
    private const int SummaryMaxLength = 300;

    public async Task<Result<ProjectStartupIndustriesResponse>> HandleAsync(
        Guid projectId,
        UpdateProjectStartupIndustriesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
            return Failure(ErrorCodes.ProjectDataAccessDenied, "Only administrators and lecturers can update project startup industries.");

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
            return Failure(ErrorCodes.ProjectDataValidationError, "A valid rowVersion is required.");

        var requestedIds = (request.StartupIndustryIds ?? []).Distinct().ToArray();
        if (requestedIds.Length is < ProjectStartupIndustryRules.MinIndustries or > ProjectStartupIndustryRules.MaxIndustries)
        {
            return Failure(ErrorCodes.ProjectDataValidationError,
                $"Select between {ProjectStartupIndustryRules.MinIndustries} and {ProjectStartupIndustryRules.MaxIndustries} startup industries.");
        }

        // The project must exist within the feature scope (administrator view) before role-specific access is judged.
        var project = await ProjectDataQuery.Scoped(context.Projects.Include(item => item.ProjectTags), currentUserId, isAdmin: true)
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
            return Failure(ErrorCodes.ProjectDataNotFound, "The project was not found.");

        if (!isAdmin && !await ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin: false)
                .AnyAsync(item => item.Id == projectId, cancellationToken))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "You can only update projects of classes assigned to you.");
        }

        if (project.Version != expectedVersion)
            return Failure(ErrorCodes.ProjectDataConcurrencyConflict, "The project was changed by another user. Refresh and try again.");

        var industries = await context.StartupIndustries.AsNoTracking()
            .Where(industry => industry.Status == StartupIndustryStatus.Active && requestedIds.Contains(industry.Id))
            .ToListAsync(cancellationToken);
        if (industries.Count != requestedIds.Length)
            return Failure(ErrorCodes.ProjectDataValidationError, "Every startup industry must exist and be active.");

        var requested = industries
            .GroupBy(industry => industry.NormalizedName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var currentTags = project.ProjectTags.Where(tag => tag.TagType == ProjectTagType.StartupField).ToArray();
        var before = ProjectDataQuery.DistinctSorted(currentTags.Select(tag => tag.TagName));

        // An unchanged set is a no-op so retries after a lost response stay harmless.
        if (currentTags.Select(tag => tag.NormalizedTagName).ToHashSet(StringComparer.Ordinal).SetEquals(requested.Keys))
            return Result.Success(ToResponse(project, before));

        var now = dateTimeProvider.UtcNow;
        foreach (var tag in currentTags.Where(tag => !requested.ContainsKey(tag.NormalizedTagName)))
        {
            context.ProjectTags.Remove(tag);
            project.ProjectTags.Remove(tag);
        }

        var currentKeys = currentTags.Select(tag => tag.NormalizedTagName).ToHashSet(StringComparer.Ordinal);
        foreach (var industry in requested.Values.Where(industry => !currentKeys.Contains(industry.NormalizedName)))
        {
            context.ProjectTags.Add(new ProjectTag
            {
                ProjectId = project.Id,
                Project = project,
                TagName = industry.Name,
                NormalizedTagName = industry.NormalizedName,
                TagType = ProjectTagType.StartupField,
                CreatedById = currentUserId,
                CreatedAt = now,
            });
        }

        // Tags live in their own table, so the project row is touched to move its version and record who changed it.
        project.UpdatedAt = now;
        project.UpdatedBy = currentUserId;

        var after = ProjectDataQuery.DistinctSorted(requested.Values.Select(industry => industry.Name));
        context.ProjectActivityLogs.Add(new ProjectActivityLog
        {
            ProjectId = project.Id,
            ActorUserId = currentUserId,
            Action = ProjectStartupIndustryHistoryParser.ChangedAction,
            Summary = BuildSummary(before, after),
            ChangedFieldsJson = JsonSerializer.Serialize(ProjectStartupIndustryHistoryParser.BuildTokens(before, after)),
            OccurredAtUtc = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ProjectDataConcurrencyConflict, "The project was changed by another user. Refresh and try again.");
        }

        return Result.Success(ToResponse(project, after));
    }

    private static ProjectStartupIndustriesResponse ToResponse(Project project, IReadOnlyCollection<string> industries) => new()
    {
        ProjectId = project.Id,
        StartupIndustries = industries,
        RowVersion = project.Version.ToString(),
    };

    /// <summary>One line for the activity log; cut so the summary always fits its column.</summary>
    private static string BuildSummary(IReadOnlyCollection<string> before, IReadOnlyCollection<string> after)
    {
        var summary = $"Startup industries changed from {Describe(before)} to {Describe(after)}.";
        return summary.Length <= SummaryMaxLength ? summary : summary[..(SummaryMaxLength - 1)] + "…";
    }

    private static string Describe(IReadOnlyCollection<string> names) =>
        names.Count == 0 ? "none" : string.Join(", ", names);

    private static Result<ProjectStartupIndustriesResponse> Failure(string code, string message) =>
        Result.Failure<ProjectStartupIndustriesResponse>(code, message);
}
