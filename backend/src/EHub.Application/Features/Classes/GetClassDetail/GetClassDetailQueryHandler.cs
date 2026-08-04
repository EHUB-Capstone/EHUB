using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
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
        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.PrimaryLecturer)
            .Include(c => c.ClassLecturers)
            .Include(c => c.ClassStudents)
            .ThenInclude(cs => cs.Student)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.NotFound", "The requested class was not found."));
        }

        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        var isStudent = string.Equals(currentUserRole, SystemRoles.Student, StringComparison.OrdinalIgnoreCase);

        if (isLecturer)
        {
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure<ClassResponse>(
                    new Error("Classes.AccessDenied", "You do not have permission to view details of this class."));
            }
        }
        else if (isStudent)
        {
            var isEnrolled = targetClass.ClassStudents.Any(cs => cs.Student.UserId == currentUserId && cs.EnrollmentStatus == EnrollmentStatus.Active);
            if (!isEnrolled)
            {
                return Result.Failure<ClassResponse>(
                    new Error("Classes.AccessDenied", "You are not enrolled in this class."));
            }
        }

        var studentCount = await _context.ClassStudents.CountAsync(cs => cs.ClassId == targetClass.Id && cs.EnrollmentStatus == EnrollmentStatus.Active, cancellationToken);
        var teamCount = await _context.Teams.CountAsync(t => t.ClassId == targetClass.Id && t.Status == TeamStatus.Active, cancellationToken);

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
            ScheduleJson = targetClass.ScheduleJson,
            IsEnrollmentMajorLocked = targetClass.IsEnrollmentMajorLocked,
            Status = targetClass.Status.ToString(),
            StudentCount = studentCount,
            TeamCount = teamCount,
            CreatedAtUtc = targetClass.CreatedAt
        };

        return Result.Success(response);
    }
}
