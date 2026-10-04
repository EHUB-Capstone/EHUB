using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectAchievementHistory;

public sealed class GetProjectAchievementHistoryQueryHandler(IApplicationDbContext context)
    : IGetProjectAchievementHistoryQueryHandler
{
    /// <summary>Entries returned per request; the total is reported so the client can say when it shows only the latest.</summary>
    internal const int MaxItems = 50;

    public async Task<Result<ProjectAchievementHistoryResponse>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "Only administrators and lecturers can view project data.");
        }

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
            .Where(log => log.ProjectId == projectId &&
                          (log.Action == ProjectAchievementMapping.ChangedAction ||
                           log.Action == ProjectAchievementMapping.CarriedOverAction));

        var totalCount = await history.CountAsync(cancellationToken);
        var items = await history
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(MaxItems)
            .Select(log => new
            {
                log.Id,
                log.Action,
                log.Summary,
                log.ChangedFieldsJson,
                ActorName = log.ActorUser == null ? null : log.ActorUser.FullName,
                log.OccurredAtUtc,
            })
            .ToListAsync(cancellationToken);

        return Result.Success(new ProjectAchievementHistoryResponse
        {
            TotalCount = totalCount,
            Items = items.Select(log =>
            {
                var change = ProjectAchievementHistoryParser.Parse(log.Action, log.Summary, log.ChangedFieldsJson);
                return new ProjectAchievementHistoryItemResponse
                {
                    Id = log.Id,
                    Action = log.Action,
                    Summary = log.Summary,
                    ActorName = log.ActorName,
                    OccurredAtUtc = log.OccurredAtUtc,
                    Added = change.Added,
                    Removed = change.Removed,
                    Kept = change.Kept,
                    NoteChanged = change.NoteChanged,
                    Note = change.Note,
                };
            }).ToList(),
        });
    }

    private static Result<ProjectAchievementHistoryResponse> Failure(string code, string message) =>
        Result.Failure<ProjectAchievementHistoryResponse>(code, message);
}
