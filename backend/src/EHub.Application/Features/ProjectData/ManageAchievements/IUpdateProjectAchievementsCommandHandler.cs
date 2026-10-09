using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.ManageAchievements;

public interface IUpdateProjectAchievementsCommandHandler
{
    Task<Result<ProjectAchievementsResponse>> HandleAsync(
        Guid projectId,
        UpdateProjectAchievementsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
