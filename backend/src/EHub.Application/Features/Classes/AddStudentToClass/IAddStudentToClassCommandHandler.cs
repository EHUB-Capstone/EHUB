using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.AddStudentToClass;

public interface IAddStudentToClassCommandHandler
{
    Task<Result<ClassStudentDto>> HandleAsync(
        Guid classId,
        AddStudentToClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
