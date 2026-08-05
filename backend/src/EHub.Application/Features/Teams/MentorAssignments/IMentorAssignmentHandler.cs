using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Teams.MentorAssignments;

public interface IMentorAssignmentHandler
{
    Task<Result<IReadOnlyCollection<MentorCandidateDto>>> GetCandidatesAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<MentorAssignmentDto>>> GetForClassAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<MentorAssignmentDto>>> GetForTeamAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<MentorAssignmentDto>> AssignAsync(Guid teamId, AssignMentorRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> EndAsync(Guid teamId, EndMentorAssignmentRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
}
