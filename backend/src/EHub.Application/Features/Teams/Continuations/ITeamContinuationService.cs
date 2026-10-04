using EHub.Contracts.Teams;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Teams.Continuations;

/// <summary>A student row of an import that is not enrolled in the target class yet.</summary>
public sealed record PendingContinuationStudent(string StudentCode, string Email, string? MajorCode);

public interface ITeamContinuationService
{
    /// <summary>Read-only simulation used by the import preview.</summary>
    Task<TeamContinuationSummaryDto> PreviewAsync(
        Class targetClass,
        IReadOnlyCollection<PendingContinuationStudent> pendingStudents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Continues eligible previous-semester teams into the target class. Must run inside the caller's
    /// transaction after the imported enrollments were saved; it saves its own changes (teams,
    /// projects, continuation records, audit log and outbox) in one SaveChanges.
    /// </summary>
    Task<TeamContinuationSummaryDto> ApplyAsync(
        Class targetClass,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
