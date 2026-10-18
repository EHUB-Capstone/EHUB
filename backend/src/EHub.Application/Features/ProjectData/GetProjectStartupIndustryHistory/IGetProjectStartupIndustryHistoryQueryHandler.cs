using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.GetProjectStartupIndustryHistory;

public interface IGetProjectStartupIndustryHistoryQueryHandler
{
    Task<Result<ProjectStartupIndustryHistoryResponse>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
