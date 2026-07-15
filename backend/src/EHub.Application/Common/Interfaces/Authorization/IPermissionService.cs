using System;
using System.Threading;
using System.Threading.Tasks;

namespace EHub.Application.Common.Interfaces.Authorization;

public interface IPermissionService
{
    Task<bool> CanAccessProjectAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> CanSubmitForTeamAsync(
        Guid userId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<bool> CanEvaluateSubmissionAsync(
        Guid userId,
        Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<bool> CanViewMentorAssignmentAsync(
        Guid userId,
        Guid mentorAssignmentId,
        CancellationToken cancellationToken = default);
}
