using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Classes.ImportStudents;

public interface IPreviewImportStudentsCommandHandler
{
    Task<Result<ImportStudentsPreviewResponse>> HandleAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
