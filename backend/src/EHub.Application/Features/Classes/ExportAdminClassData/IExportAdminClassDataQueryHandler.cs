using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.ExportAdminClassData;

public interface IExportAdminClassDataQueryHandler
{
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        ExportAdminClassDataRequest request,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
