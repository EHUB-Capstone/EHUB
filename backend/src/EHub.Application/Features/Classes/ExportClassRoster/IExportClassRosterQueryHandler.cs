using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.ExportClassRoster;

public interface IExportClassRosterQueryHandler
{
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
