using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.UpdateClassStudent;

public interface IUpdateClassStudentCommandHandler
{
    Task<Result<ClassStudentDto>> HandleAsync(
        Guid classId,
        Guid studentId,
        UpdateClassStudentRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
