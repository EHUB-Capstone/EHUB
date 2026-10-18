using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.ManageStartupIndustries;

public interface IUpdateProjectStartupIndustriesCommandHandler
{
    Task<Result<ProjectStartupIndustriesResponse>> HandleAsync(
        Guid projectId,
        UpdateProjectStartupIndustriesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
