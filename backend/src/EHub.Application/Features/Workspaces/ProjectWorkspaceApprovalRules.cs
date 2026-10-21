using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces;

public static class ProjectWorkspaceApprovalRules
{
    // Legacy and continued projects without a direction retain their existing access.
    // A later profile change must not lock a workspace that has already been approved.
    public static bool IsApproved(ProjectDirection? direction) =>
        direction == null || direction.Status == ProjectDirectionStatus.Approved ||
        direction.Reviews.Any(review => review.ToStatus == ProjectDirectionStatus.Approved);

    public static async Task<bool> IsApprovedAsync(
        IApplicationDbContext context, Guid teamId, CancellationToken cancellationToken = default) =>
        !await context.ProjectDirections.AsNoTracking().AnyAsync(direction =>
            direction.TeamId == teamId && direction.Status != ProjectDirectionStatus.Approved &&
            !direction.Reviews.Any(review => review.ToStatus == ProjectDirectionStatus.Approved), cancellationToken);
}
