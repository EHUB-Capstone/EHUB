using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.SetEnrollmentMajorLock;

public sealed class SetEnrollmentMajorLockCommandHandler : ISetEnrollmentMajorLockCommandHandler
{
    private readonly IApplicationDbContext _context;

    public SetEnrollmentMajorLockCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<EnrollmentMajorLockResponse>> HandleAsync(
        Guid classId,
        bool shouldLock,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isStaff = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isStaff)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or lecturer can lock or unlock enrollment majors.");
        }

        var targetClass = await _context.Classes
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (targetClass.Status == ClassStatus.Archived && shouldLock)
        {
            return Failure(ErrorCodes.ClassArchived, "An archived class cannot enforce an enrollment major lock.");
        }

        var effectiveTarget = targetClass.Status == ClassStatus.Archived ? false : shouldLock;
        if (targetClass.IsEnrollmentMajorLocked == effectiveTarget)
        {
            return Result.Success(new EnrollmentMajorLockResponse
            {
                ClassId = classId,
                IsLocked = effectiveTarget
            });
        }

        targetClass.IsEnrollmentMajorLocked = effectiveTarget;
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = classId,
            Action = effectiveTarget ? "ENROLLMENT_MAJOR_LOCKED" : "ENROLLMENT_MAJOR_UNLOCKED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { IsLocked = effectiveTarget })
        });
        ClassOutbox.Enqueue(_context, "Class.EnrollmentMajorLockChanged.v1", classId, new
        {
            IsLocked = effectiveTarget
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class changed concurrently. Refresh and try again.");
        }

        return Result.Success(new EnrollmentMajorLockResponse
        {
            ClassId = classId,
            IsLocked = effectiveTarget
        });
    }

    private static Result<EnrollmentMajorLockResponse> Failure(string code, string message) =>
        Result.Failure<EnrollmentMajorLockResponse>(new Error(code, message));
}
