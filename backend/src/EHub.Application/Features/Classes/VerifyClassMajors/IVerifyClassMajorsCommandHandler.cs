using EHub.Contracts.Classes;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Classes.VerifyClassMajors;

public interface IVerifyClassMajorsCommandHandler
{
    Task<Result<VerifyClassMajorsResponse>> PreviewAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<VerifyClassMajorsResponse>> HandleAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<VerifyClassMajorsResponse>> SynchronizeAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
