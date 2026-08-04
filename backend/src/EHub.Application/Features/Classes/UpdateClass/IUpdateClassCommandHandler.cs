using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.UpdateClass;

public interface IUpdateClassCommandHandler
{
    Task<Result<ClassResponse>> HandleAsync(
        Guid classId,
        UpdateClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<ClassResponse>> UpdateTeachingAssignmentAsync(
        Guid classId,
        UpdateTeachingAssignmentRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
