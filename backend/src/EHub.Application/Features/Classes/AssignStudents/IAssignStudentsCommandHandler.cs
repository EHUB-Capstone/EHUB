using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.AssignStudents;

public interface IAssignStudentsCommandHandler
{
    Task<Result<ClassStudentAssignmentResponse>> AssignToClassAsync(
        Guid classId,
        AssignStudentsToClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<TeamStudentAssignmentResponse>> AssignToTeamAsync(
        Guid classId,
        Guid teamId,
        AssignStudentsToTeamRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
