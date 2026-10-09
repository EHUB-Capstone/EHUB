using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ClassAudit;

public sealed class GetClassAuditQueryHandler : IGetClassAuditQueryHandler
{
    private readonly IApplicationDbContext _context;

    public GetClassAuditQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<ClassAuditLogListResponse>> HandleAsync(
        Guid classId,
        int page,
        int pageSize,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!ClassAuthorizationRules.IsStaff(currentUserRole))
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can inspect the class audit trail.");
        if (page < 1 || pageSize is < 1 or > 100)
            return Failure(ErrorCodes.ClassValidationError, "page must be at least 1 and pageSize must be between 1 and 100.");

        var targetClass = await _context.Classes.AsNoTracking()
            .Where(item => item.Id == classId)
            .Select(item => new { item.PrimaryLecturerId })
            .FirstOrDefaultAsync(cancellationToken);
        if (targetClass == null)
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        if (!ClassAuthorizationRules.CanManageClass(
                targetClass.PrimaryLecturerId,
                currentUserId,
                currentUserRole))
            return Failure(ErrorCodes.ClassAccessDenied, "You can only inspect the audit trail for classes assigned to you.");

        var query = _context.ClassAuditLogs.AsNoTracking().Where(item => item.ClassId == classId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ClassAuditLogDto
            {
                Id = item.Id,
                Action = item.Action,
                PerformedByUserId = item.PerformedByUserId,
                PerformedByName = item.PerformedByUser.FullName,
                OccurredAtUtc = item.OccurredAtUtc,
                DetailsJson = item.DetailsJson
            })
            .ToListAsync(cancellationToken);

        return Result.Success(new ClassAuditLogListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private static Result<ClassAuditLogListResponse> Failure(string code, string message) =>
        Result.Failure<ClassAuditLogListResponse>(new Error(code, message));
}
