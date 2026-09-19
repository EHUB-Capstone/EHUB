using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.ExportClassRoster;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ExportAdminClassData;

public sealed class ExportAdminClassDataQueryHandler : IExportAdminClassDataQueryHandler
{
    private readonly IApplicationDbContext _context;

    public ExportAdminClassDataQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        ExportAdminClassDataRequest request,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator can export data for multiple classes.");
        }

        if (!TryParseSemester(request.Semester, out var semester) || request.Year is < 2000 or > 2100)
        {
            return Failure(ErrorCodes.ClassValidationError, "Semester must be SP, SU, or FA and year must be between 2000 and 2100.");
        }

        var classIds = request.ClassIds?.ToArray() ?? Array.Empty<Guid>();
        if (classIds.Length == 0)
        {
            return Failure(ErrorCodes.ClassValidationError, "Select at least one class to export.");
        }

        if (classIds.Any(id => id == Guid.Empty))
        {
            return Failure(ErrorCodes.ClassValidationError, "Class IDs must be valid non-empty identifiers.");
        }

        if (classIds.Distinct().Count() != classIds.Length)
        {
            return Failure(ErrorCodes.ClassValidationError, "Duplicate class IDs are not allowed.");
        }

        var classes = await _context.Classes
            .AsNoTracking()
            .Include(@class => @class.Course)
            .Include(@class => @class.Semester)
            .Where(@class => classIds.Contains(@class.Id))
            .ToListAsync(cancellationToken);

        if (classes.Count != classIds.Length)
        {
            return Failure(ErrorCodes.ClassNotFound, "One or more selected classes were not found.");
        }

        if (classes.Any(@class => @class.Semester.Term != semester || @class.Semester.Year != request.Year))
        {
            return Failure(ErrorCodes.ClassValidationError, "Every selected class must belong to the requested semester and year.");
        }

        var orderedClasses = AdminClassExportOrdering.Apply(classes);

        var completedClassIds = orderedClasses
            .Where(UsesCompletedRoster)
            .Select(@class => @class.Id)
            .ToArray();
        var activeClassIds = orderedClasses
            .Where(@class => !UsesCompletedRoster(@class))
            .Select(@class => @class.Id)
            .ToArray();

        var roster = await _context.ClassStudents
            .AsNoTracking()
            .Include(enrollment => enrollment.Student)
            .Include(enrollment => enrollment.TeamMembers)
                .ThenInclude(member => member.Team)
                    .ThenInclude(team => team.Project)
            .Where(enrollment =>
                (activeClassIds.Contains(enrollment.ClassId) && enrollment.EnrollmentStatus == EnrollmentStatus.Active) ||
                (completedClassIds.Contains(enrollment.ClassId) && enrollment.EnrollmentStatus == EnrollmentStatus.Completed))
            .ToListAsync(cancellationToken);

        var rosterByClass = roster
            .GroupBy(enrollment => enrollment.ClassId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var sections = orderedClasses
            .Select(@class => new ClassRosterExportSection(
                @class,
                rosterByClass.GetValueOrDefault(@class.Id) ?? Array.Empty<ClassStudent>()))
            .ToArray();

        var bytes = ClassRosterExportWorkbookBuilder.Build(sections);
        var semesterCode = ToSemesterCode(semester);
        var fileName = $"{semesterCode}{request.Year}_class_data.xlsx";

        return Result.Success((bytes, ClassRosterExportWorkbookBuilder.ContentType, fileName));
    }

    private static bool UsesCompletedRoster(Class @class) =>
        @class.Status == ClassStatus.Completed ||
        @class.Status == ClassStatus.Archived && @class.StatusBeforeArchive == ClassStatus.Completed;

    private static bool TryParseSemester(string? value, out SemesterTerm semester)
    {
        semester = value?.Trim().ToUpperInvariant() switch
        {
            "SP" or "SPRING" => SemesterTerm.Spring,
            "SU" or "SUMMER" => SemesterTerm.Summer,
            "FA" or "FALL" => SemesterTerm.Fall,
            _ => default
        };
        return value?.Trim().ToUpperInvariant() is "SP" or "SPRING" or "SU" or "SUMMER" or "FA" or "FALL";
    }

    private static string ToSemesterCode(SemesterTerm semester) => semester switch
    {
        SemesterTerm.Spring => "SP",
        SemesterTerm.Summer => "SU",
        SemesterTerm.Fall => "FA",
        _ => string.Empty
    };

    private static Result<(byte[], string, string)> Failure(string code, string message) =>
        Result.Failure<(byte[], string, string)>(new Error(code, message));
}
