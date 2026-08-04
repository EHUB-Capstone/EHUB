using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.CreateBulkClasses;

public interface ICreateBulkClassesCommandHandler
{
    Task<Result<BulkClassPreviewResponse>> PreviewAsync(
        CreateBulkClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<ClassResponse>>> CommitAsync(
        CreateBulkClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
