using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Shared.Results;
using EHub.Contracts.Classes;

namespace EHub.Application.Features.Classes.ExportClassRoster;

public interface IExportClassRosterQueryHandler
{
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        Guid classId,
        ExportClassRosterRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
