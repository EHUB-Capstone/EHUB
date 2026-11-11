using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.GetProjectEvidence;

public interface IGetProjectEvidenceQueryHandler
{
    Task<Result<ProjectEvidenceResponse>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
