using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces;

public interface IProjectWorkspaceHandler
{
    Task<Result<IReadOnlyCollection<WorkspaceOptionDto>>> GetAccessibleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceContextDto?>> GetCurrentContextAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceContextDto>> GetContextAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkspaceDetailDto>> GetDetailAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkspaceDto>> CreateAsync(Guid teamId, CreateProjectWorkspaceRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkspaceDto>> UpdateAsync(Guid teamId, UpdateProjectWorkspaceRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
}
