using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Authorization;

namespace EHub.Application.Common.Services.Authorization;

public sealed class PermissionService : IPermissionService
{
    public Task<bool> CanAccessProjectAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // Admin: can access all projects.
        // Student: can access project of own team.
        // Lecturer: can access projects in assigned classes.
        // Mentor: can access assigned mentored projects.

        return Task.FromResult(false);
    }

    public Task<bool> CanSubmitForTeamAsync(
        Guid userId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // Student can submit only for own team.
        // Admin/Lecturer may have override depending on business rules.

        return Task.FromResult(false);
    }

    public Task<bool> CanEvaluateSubmissionAsync(
        Guid userId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // Lecturer can evaluate submissions assigned to them.
        // Admin can evaluate or override if policy allows.

        return Task.FromResult(false);
    }

    public Task<bool> CanViewMentorAssignmentAsync(
        Guid userId,
        Guid mentorAssignmentId,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // Mentor can view own assignments.
        // Admin/Lecturer can view depending on module rules.

        return Task.FromResult(false);
    }
}
