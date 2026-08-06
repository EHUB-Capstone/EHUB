using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.GetClassDetail;

public sealed class GetClassDetailQueryHandler : IGetClassDetailQueryHandler
{
    private readonly IApplicationDbContext _context;

    public GetClassDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        var isStudent = string.Equals(currentUserRole, SystemRoles.Student, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer && !isStudent)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassAccessDenied, "You do not have permission to view details of this class."));
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.PrimaryLecturer)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);


        if (targetClass == null)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
        }

        if (isStudent)
        {
            var isEnrolled = await _context.ClassStudents.AsNoTracking().AnyAsync(
                cs => cs.ClassId == classId &&
                    cs.Student.UserId == currentUserId &&
                    cs.EnrollmentStatus == EnrollmentStatus.Active,
                cancellationToken);
            if (!isEnrolled)
            {
                return Result.Failure<ClassResponse>(
                    new Error(ErrorCodes.ClassAccessDenied, "You are not enrolled in this class."));
            }
        }
        var studentCount = await _context.ClassStudents.CountAsync(cs => cs.ClassId == targetClass.Id && cs.EnrollmentStatus == EnrollmentStatus.Active, cancellationToken);
        var teamCount = await _context.Teams.CountAsync(t => t.ClassId == targetClass.Id && t.Status == TeamStatus.Active, cancellationToken);
        var mentors = await _context.MentorAssignments.AsNoTracking()
            .Where(assignment =>
                assignment.Team.ClassId == targetClass.Id &&
                assignment.Team.Status == TeamStatus.Active &&
                assignment.Status == MentorAssignmentStatus.Active &&
                assignment.EndedAt == null)
            .Select(assignment => new ClassMentorSummaryDto
            {
                MentorProfileId = assignment.MentorProfileId,
                UserId = assignment.MentorProfile.UserId,
                FullName = assignment.MentorProfile.User.FullName,
                Email = assignment.MentorProfile.User.Email
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var response = new ClassResponse
        {
            Id = targetClass.Id,
            ClassCode = targetClass.ClassCode,
            ClassIndex = targetClass.ClassIndex,
            CourseId = targetClass.CourseId,
            SubjectCode = targetClass.Course.Code,
            SubjectName = targetClass.Course.Name,
            SemesterId = targetClass.SemesterId,
            SemesterCode = targetClass.Semester.Code,
            Year = targetClass.Semester.Year,
            PrimaryLecturerId = targetClass.PrimaryLecturerId,
            PrimaryLecturerName = targetClass.PrimaryLecturer?.FullName,
            PrimaryLecturerEmail = targetClass.PrimaryLecturer?.Email,
            Room = targetClass.Room,
            Schedules = ClassScheduleRules.Deserialize(targetClass.ScheduleJson),
            IsEnrollmentMajorLocked = targetClass.Status != ClassStatus.Archived && targetClass.IsEnrollmentMajorLocked,
            Status = targetClass.Status.ToString(),
            StudentCount = studentCount,
            TeamCount = teamCount,
            Mentors = mentors,
            CreatedAtUtc = targetClass.CreatedAt,
            RowVersion = targetClass.Version.ToString()
        };

        return Result.Success(response);
    }
}
