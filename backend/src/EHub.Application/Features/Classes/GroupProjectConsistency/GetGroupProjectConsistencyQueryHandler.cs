using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.GroupProjectConsistency;

public sealed class GetGroupProjectConsistencyQueryHandler : IGetGroupProjectConsistencyQueryHandler
{
    private readonly IApplicationDbContext _context;

    public GetGroupProjectConsistencyQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GroupProjectConsistencyResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to view this class.");
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(item => item.ClassLecturers)
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (isLecturer &&
            targetClass.PrimaryLecturerId != currentUserId &&
            !targetClass.ClassLecturers.Any(item => item.LecturerId == currentUserId))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only view classes assigned to you.");
        }

        // The Project of a student is the project of their active team.
        var rows = await _context.ClassStudents
            .AsNoTracking()
            .Where(enrollment => enrollment.ClassId == classId &&
                                 enrollment.EnrollmentStatus == EnrollmentStatus.Active &&
                                 enrollment.SemesterGroupName != null)
            .Select(enrollment => new
            {
                Group = enrollment.SemesterGroupName,
                Project = enrollment.TeamMembers
                    .Where(member => member.CountsTowardActiveTeam &&
                                     member.Team.Status == TeamStatus.Active &&
                                     member.Team.Project != null)
                    .Select(member => member.Team.Project!.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var warnings = GroupProjectConsistencyRules.Evaluate(rows.Select(row => (row.Group, row.Project)));
        return Result.Success(new GroupProjectConsistencyResponse
        {
            IsConsistent = warnings.Count == 0,
            Warnings = warnings
        });
    }

    private static Result<GroupProjectConsistencyResponse> Failure(string code, string message) =>
        Result.Failure<GroupProjectConsistencyResponse>(new Error(code, message));
}
