using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.DropAllStudents;

public sealed class DropAllStudentsCommandHandler : IDropAllStudentsCommandHandler
{
    private readonly IApplicationDbContext _context;

    public DropAllStudentsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DropAllStudentsResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to remove students from this class.");
        }

        var targetClass = await _context.Classes
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null) return Result.Failure<DropAllStudentsResponse>(mutationError);

        if (isLecturer && targetClass.PrimaryLecturerId != currentUserId)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only remove students from classes assigned to you.");
        }

        var hasActiveTeamMembers = await _context.TeamMembers.AnyAsync(member =>
            member.ClassId == classId &&
            member.CountsTowardActiveTeam &&
            member.Team.Status == TeamStatus.Active,
            cancellationToken);
        if (hasActiveTeamMembers)
        {
            return Failure(
                ErrorCodes.ClassStudentInActiveTeam,
                "Cannot remove all students while the class has active teams. Dissolve the teams first.");
        }

        var hasOpenProposalMembers = await _context.TeamProposalMembers.AnyAsync(member =>
            member.ClassId == classId && member.CountsTowardOpenProposal,
            cancellationToken);
        if (hasOpenProposalMembers)
        {
            return Failure(
                ErrorCodes.TeamMembershipConflict,
                "Cannot remove all students while the class has open team proposals. Resolve or cancel the proposals first.");
        }

        var activeEnrollments = await _context.ClassStudents
            .Where(item => item.ClassId == classId && item.EnrollmentStatus == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
        if (activeEnrollments.Count == 0)
        {
            return Result.Success(new DropAllStudentsResponse { DroppedCount = 0 });
        }

        var now = DateTime.UtcNow;
        foreach (var enrollment in activeEnrollments)
        {
            enrollment.EnrollmentStatus = EnrollmentStatus.Dropped;
            enrollment.CountsTowardCourseSemesterLimit = false;
            enrollment.UpdatedAt = now;
            ClassOutbox.Enqueue(_context, "Class.StudentEnrollmentDropped.v1", classId, new
            {
                StudentId = enrollment.StudentId
            }, now);
        }

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = classId,
            Action = "ALL_STUDENT_ENROLLMENTS_DROPPED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = now,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { DroppedCount = activeEnrollments.Count })
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(
                ErrorCodes.ClassConcurrencyConflict,
                "The roster changed concurrently. Refresh the class and try again.");
        }

        return Result.Success(new DropAllStudentsResponse { DroppedCount = activeEnrollments.Count });
    }

    private static Result<DropAllStudentsResponse> Failure(string code, string message) =>
        Result.Failure<DropAllStudentsResponse>(new Error(code, message));
}
