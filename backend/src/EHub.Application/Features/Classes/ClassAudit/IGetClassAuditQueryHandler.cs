using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.ClassAudit;

public interface IGetClassAuditQueryHandler
{
    Task<Result<ClassAuditLogListResponse>> HandleAsync(
        Guid classId,
        int page,
        int pageSize,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
