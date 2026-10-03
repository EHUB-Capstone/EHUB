using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.EvaluationReportExport;

public interface IEvaluationReportExportHandler
{
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        EvaluationReportExportRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
