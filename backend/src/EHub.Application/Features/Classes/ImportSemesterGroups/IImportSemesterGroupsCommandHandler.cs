using EHub.Contracts.Classes;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Classes.ImportSemesterGroups;

public interface IImportSemesterGroupsCommandHandler
{
    Task<Result<SemesterGroupImportResponse>> PreviewAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<SemesterGroupImportResponse>> ImportAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
