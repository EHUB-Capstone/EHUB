using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Teams.ProjectDirections;

public interface IProjectDirectionHandler
{
    Task<Result<ProjectDirectionDto>> GetAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectDirectionDto>> SaveAsync(Guid teamId, SaveProjectDirectionRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectDirectionDto>> SubmitAsync(Guid teamId, ProjectDirectionStateRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectDirectionDto>> ReviewAsync(Guid teamId, ReviewProjectDirectionRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
}
