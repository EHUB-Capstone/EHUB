using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces;

public interface IWorkspaceToolsHandler
{
    Task<Result<WeeklyTaskBoardDto>> GetWeeklyTasksAsync(WeeklyTaskQuery query, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<WeeklyTaskDto>> CreateWeeklyTaskAsync(SaveWeeklyTaskRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<WeeklyTaskDto>> UpdateWeeklyTaskAsync(Guid taskId, SaveWeeklyTaskRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<WeeklyTaskDto>> UpdateWeeklyTaskStatusAsync(Guid taskId, UpdateWeeklyTaskStatusRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteWeeklyTaskAsync(Guid taskId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<ProjectShortcutDto>>> GetShortcutsAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectShortcutDto>> CreateShortcutAsync(Guid teamId, SaveShortcutRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectShortcutDto>> UpdateShortcutAsync(Guid teamId, Guid shortcutId, SaveShortcutRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteShortcutAsync(Guid teamId, Guid shortcutId, Guid userId, string role, CancellationToken cancellationToken = default);
}
