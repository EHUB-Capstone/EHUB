using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.GetClasses;

public interface IGetClassesQueryHandler
{
    Task<Result<ClassListResponse>> HandleAsync(
        GetClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
