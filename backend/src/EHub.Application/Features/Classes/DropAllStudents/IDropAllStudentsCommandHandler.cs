using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.DropAllStudents;

public interface IDropAllStudentsCommandHandler
{
    Task<Result<DropAllStudentsResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
