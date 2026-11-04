using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.ManageCompetitions;

public interface IUpdateProjectCompetitionsCommandHandler
{
    Task<Result<ProjectCompetitionsResponse>> HandleAsync(
        Guid projectId,
        UpdateProjectCompetitionsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
