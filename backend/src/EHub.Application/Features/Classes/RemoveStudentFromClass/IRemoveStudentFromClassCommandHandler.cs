using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.RemoveStudentFromClass;

public interface IRemoveStudentFromClassCommandHandler
{
    Task<Result> HandleAsync(
        Guid classId,
        Guid studentId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
