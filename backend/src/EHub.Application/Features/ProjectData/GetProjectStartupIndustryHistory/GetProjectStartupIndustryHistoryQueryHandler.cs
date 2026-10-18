using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectStartupIndustryHistory;

public sealed class GetProjectStartupIndustryHistoryQueryHandler(IApplicationDbContext context)
    : IGetProjectStartupIndustryHistoryQueryHandler
{
    /// <summary>Entries returned per request; the total is reported so the client can say when it shows only the latest.</summary>
    internal const int MaxItems = 50;

    public async Task<Result<ProjectStartupIndustryHistoryResponse>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
            return Failure(ErrorCodes.ProjectDataAccessDenied, "Only administrators and lecturers can view project data.");

        // Same rule as editing: the project must be in the feature scope, and a lecturer must teach its class.
        if (!await ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin: true)
                .AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return Failure(ErrorCodes.ProjectDataNotFound, "The project was not found.");
        }

        if (!isAdmin && !await ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin: false)
                .AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "You can only view projects of classes assigned to you.");
        }

        var history = context.ProjectActivityLogs.AsNoTracking()
            .Where(log => log.ProjectId == projectId && log.Action == ProjectStartupIndustryHistoryParser.ChangedAction);

        var totalCount = await history.CountAsync(cancellationToken);
        var items = await history
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(MaxItems)
            .Select(log => new
            {
                log.Id,
                log.Summary,
                log.ChangedFieldsJson,
                ActorName = log.ActorUser == null ? null : log.ActorUser.FullName,
                log.OccurredAtUtc,
            })
            .ToListAsync(cancellationToken);

        return Result.Success(new ProjectStartupIndustryHistoryResponse
        {
            TotalCount = totalCount,
            Items = items.Select(log =>
            {
                var change = ProjectStartupIndustryHistoryParser.Parse(log.ChangedFieldsJson);
                return new ProjectStartupIndustryHistoryItemResponse
                {
                    Id = log.Id,
                    Summary = log.Summary,
                    ActorName = log.ActorName,
                    OccurredAtUtc = log.OccurredAtUtc,
                    Added = change.Added,
                    Removed = change.Removed,
                    Kept = change.Kept,
                };
            }).ToList(),
        });
    }

    private static Result<ProjectStartupIndustryHistoryResponse> Failure(string code, string message) =>
        Result.Failure<ProjectStartupIndustryHistoryResponse>(code, message);
}
