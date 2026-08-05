using EHub.Contracts.Classes;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Classes.VerifyClassMajors;

public interface IVerifyClassMajorsCommandHandler
{
    Task<Result<VerifyClassMajorsResponse>> HandleAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
