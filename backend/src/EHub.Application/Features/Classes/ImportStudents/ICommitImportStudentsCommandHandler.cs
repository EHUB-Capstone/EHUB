using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.ImportStudents;

public interface ICommitImportStudentsCommandHandler
{
    Task<Result<ImportStudentsCommitResponse>> HandleAsync(
        Guid classId,
        CommitImportStudentsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
