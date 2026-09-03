using EHub.Contracts.Users;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Admin.Users.ImportLecturers;

public interface ILecturerImportHandler
{
    Task<Result<LecturerImportPreviewResponse>> PreviewAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<Result<LecturerImportCommitResponse>> CommitAsync(
        CommitLecturerImportRequest request,
        CancellationToken cancellationToken = default);
}
