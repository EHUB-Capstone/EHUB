using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.ReEnrollStudent;

public interface IReEnrollStudentCommandHandler
{
    Task<Result<ClassStudentDto>> HandleAsync(
        Guid classId,
        Guid studentId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
