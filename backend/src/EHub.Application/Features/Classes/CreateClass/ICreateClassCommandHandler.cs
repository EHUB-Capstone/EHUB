using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.CreateClass;

public interface ICreateClassCommandHandler
{
    Task<Result<ClassResponse>> HandleAsync(
        CreateClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
