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

namespace EHub.Application.Features.Classes.ExportClassRoster;

public sealed class ExportClassRosterQueryHandler : IExportClassRosterQueryHandler
{
    private readonly IApplicationDbContext _context;

    public ExportClassRosterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        Guid classId,
        ExportClassRosterRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassAccessDenied, "You do not have permission to export class roster."));
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);


        if (targetClass == null)
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
        }

        if (isLecturer)
        {
            if (targetClass.PrimaryLecturerId != currentUserId)
            {
                return Result.Failure<(byte[], string, string)>(
                    new Error(ErrorCodes.ClassAccessDenied, "You can only export roster for classes assigned to you."));
            }
        }

        var normalizedScope = request.Scope.Trim();
        if (!string.Equals(normalizedScope, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedScope, "History", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassValidationError, "Export scope must be Active or History."));
        }

        EnrollmentStatus? status;
        if (string.Equals(normalizedScope, "Active", StringComparison.OrdinalIgnoreCase))
        {
            status = EnrollmentStatus.Active;
            if (!string.IsNullOrWhiteSpace(request.Status) &&
                !string.Equals(request.Status, nameof(EnrollmentStatus.Active), StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<(byte[], string, string)>(
                    new Error(ErrorCodes.ClassValidationError, "Active export scope only accepts Active enrollment status."));
            }
        }
        else if (!ClassRosterFilters.TryParseStatus(request.Status, out status))
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassValidationError, "Enrollment status must be Active, Dropped, or Completed."));
        }

        var rosterQuery = _context.ClassStudents
            .AsNoTracking()
            .Include(cs => cs.Student)
            .Include(cs => cs.TeamMembers)
                .ThenInclude(tm => tm.Team)
                    .ThenInclude(t => t.Project)
            .Where(cs => cs.ClassId == classId);


        rosterQuery = ClassRosterFilters.Apply(rosterQuery, request.Search, request.MajorCode, status);

        var roster = await rosterQuery
            .OrderBy(cs => cs.Student.RollNumber)
            .ThenBy(cs => cs.Student.FullName)
            .ToListAsync(cancellationToken);

        var registeredMajorByEmail = await RegisteredStudentMajorResolver.LoadByEmailAsync(
            _context, roster.Select(enrollment => enrollment.Student.Email), cancellationToken);
        var mentorsByTeam = await ClassRosterMentorResolver.LoadByTeamAsync(
            _context, [classId], cancellationToken);

        var bytes = ClassRosterExportWorkbookBuilder.Build(
        [
            new ClassRosterExportSection(targetClass, roster, mentorsByTeam)
        ], registeredMajorByEmail);

        var scopeSuffix = string.Equals(normalizedScope, "Active", StringComparison.OrdinalIgnoreCase)
            ? "active"
            : "history";
        var fileName = $"{targetClass.ClassCode}_students_{scopeSuffix}.xlsx";
        var contentType = ClassRosterExportWorkbookBuilder.ContentType;

        return Result.Success((bytes, contentType, fileName));
    }
}
