using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.GetProjectAchievementHistory;

public interface IGetProjectAchievementHistoryQueryHandler
{
    Task<Result<ProjectAchievementHistoryResponse>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
