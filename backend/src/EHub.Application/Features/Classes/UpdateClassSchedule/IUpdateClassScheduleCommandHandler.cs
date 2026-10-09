using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.UpdateClassSchedule;

public interface IUpdateClassScheduleCommandHandler
{
    Task<Result<ClassResponse>> HandleAsync(
        Guid classId,
        UpdateClassScheduleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
