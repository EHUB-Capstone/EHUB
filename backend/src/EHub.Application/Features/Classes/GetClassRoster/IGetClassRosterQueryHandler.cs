using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.GetClassRoster;

public interface IGetClassRosterQueryHandler
{
    Task<Result<ClassRosterListResponse>> HandleAsync(
        Guid classId,
        GetClassRosterRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
