using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.CreateBulkClasses;

public sealed class CreateBulkClassesCommandHandler : ICreateBulkClassesCommandHandler
{
    private const int MaximumBatchSize = 100;
    private const int MaximumClassIndex = 999;
    private readonly IApplicationDbContext _context;

    public CreateBulkClassesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BulkClassPreviewResponse>> PreviewAsync(
        CreateBulkClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateCommonAsync(request, currentUserId, currentUserRole, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<BulkClassPreviewResponse>(validation.Error);
        }

        return Result.Success(await BuildPreviewAsync(validation.Value, cancellationToken));
    }

    public async Task<Result<IReadOnlyCollection<ClassResponse>>> CommitAsync(
        CreateBulkClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateCommonAsync(request, currentUserId, currentUserRole, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<ClassResponse>>(validation.Error);
        }

        var preview = await BuildPreviewAsync(validation.Value, cancellationToken);
        if (preview.InvalidCount > 0)
        {
            var firstError = preview.Items.First(item => !item.IsValid).ErrorMessage;
            return Result.Failure<IReadOnlyCollection<ClassResponse>>(
                new Error(ErrorCodes.ClassBulkCreateInvalid, $"Batch creation failed: {firstError}"));
        }

        var (course, semester, lecturer, targetIndices) = validation.Value;
        var createdClasses = new List<Class>(targetIndices.Count);
        foreach (var classIndex in targetIndices)
        {
            var newClass = new Class
            {
                ClassCode = BuildClassCode(course.Code, classIndex),
                ClassIndex = classIndex,
                SemesterId = semester.Id,
                CourseId = course.Id,
                PrimaryLecturerId = lecturer?.Id,
                Status = ClassStatus.Draft,
                CreatedById = currentUserId
            };

            createdClasses.Add(newClass);
            _context.Classes.Add(newClass);

            if (lecturer != null)
            {
                _context.ClassLecturers.Add(new ClassLecturer
                {
                    ClassId = newClass.Id,
                    LecturerId = lecturer.Id,
                    IsPrimary = true,
                    AssignedAt = DateTime.UtcNow,
                    AssignedById = currentUserId
                });
            }

            _context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = newClass.Id,
                Action = "CLASS_CREATED",
                PerformedByUserId = currentUserId,
                OccurredAtUtc = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    newClass.ClassCode,
                    newClass.CourseId,
                    newClass.SemesterId,
                    newClass.PrimaryLecturerId,
                    Batch = true
                })
            });
            ClassOutbox.Enqueue(_context, "Class.Created.v1", newClass.Id, new
            {
                newClass.ClassCode,
                newClass.PrimaryLecturerId,
                Batch = true
            });
        }

        try
        {
            // One SaveChanges call is one EF Core transaction: the batch is all-or-nothing.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.ClearChanges();
            return Result.Failure<IReadOnlyCollection<ClassResponse>>(
                new Error(ErrorCodes.ClassBulkCreateInvalid, "The batch conflicted with another class creation. Preview again and retry."));
        }

        var responses = createdClasses.Select(newClass => new ClassResponse
        {
            Id = newClass.Id,
            ClassCode = newClass.ClassCode,
            ClassIndex = newClass.ClassIndex,
            CourseId = newClass.CourseId,
            SubjectCode = course.Code,
            SubjectName = course.Name,
            SemesterId = newClass.SemesterId,
            SemesterCode = semester.Code,
            Year = semester.Year,
            PrimaryLecturerId = lecturer?.Id,
            PrimaryLecturerName = lecturer?.FullName,
            PrimaryLecturerEmail = lecturer?.Email,
            Status = newClass.Status.ToString(),
            StudentCount = 0,
            TeamCount = 0,
            CreatedAtUtc = newClass.CreatedAt,
            RowVersion = newClass.Version.ToString()
        }).ToArray();

        return Result.Success<IReadOnlyCollection<ClassResponse>>(responses);
    }

    private async Task<BulkClassPreviewResponse> BuildPreviewAsync(
        (Course Course, Semester Semester, User? Lecturer, List<int> TargetIndices) input,
        CancellationToken cancellationToken)
    {
        var (course, semester, lecturer, targetIndices) = input;
        var existingCodes = await _context.Classes
            .AsNoTracking()
            .Where(@class => @class.SemesterId == semester.Id)
            .Select(@class => @class.ClassCode)
            .ToListAsync(cancellationToken);
        var existingIndices = await _context.Classes
            .AsNoTracking()
            .Where(@class => @class.SemesterId == semester.Id && @class.CourseId == course.Id)
            .Select(@class => @class.ClassIndex)
            .ToListAsync(cancellationToken);

        var codeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var indexSet = existingIndices.ToHashSet();
        var items = targetIndices.Select(classIndex =>
        {
            var classCode = BuildClassCode(course.Code, classIndex);
            var error = codeSet.Contains(classCode)
                ? $"Class code '{classCode}' already exists in this semester."
                : indexSet.Contains(classIndex)
                    ? $"Class index {classIndex} already exists for this subject in the semester."
                    : null;

            return new BulkClassPreviewItem
            {
                ClassCode = classCode,
                ClassIndex = classIndex,
                SubjectCode = course.Code,
                SemesterCode = semester.Code,
                PrimaryLecturerName = lecturer?.FullName,
                IsValid = error == null,
                ErrorMessage = error
            };
        }).ToArray();

        return new BulkClassPreviewResponse
        {
            Items = items,
            TotalCount = items.Length,
            ValidCount = items.Count(item => item.IsValid),
            InvalidCount = items.Count(item => !item.IsValid)
        };
    }

    private async Task<Result<(Course Course, Semester Semester, User? Lecturer, List<int> TargetIndices)>> ValidateCommonAsync(
        CreateBulkClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to bulk create classes.");
        }

        var targetIndicesResult = BuildTargetIndices(request);
        if (targetIndicesResult.IsFailure)
        {
            return Result.Failure<(Course, Semester, User?, List<int>)>(targetIndicesResult.Error);
        }

        Course? course = null;
        if (request.CourseId != Guid.Empty)
        {
            course = await _context.Courses.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.CourseId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.SubjectCode))
        {
            var subjectCode = request.SubjectCode.Trim().ToUpperInvariant();
            course = await _context.Courses.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Code == subjectCode, cancellationToken);
        }

        if (course == null || course.Status != CourseStatus.Active)
        {
            return Failure(ErrorCodes.ClassValidationError, "The specified subject does not exist or is inactive.");
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectCode) &&
            !string.Equals(course.Code, request.SubjectCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Failure(ErrorCodes.ClassValidationError, "courseId and subjectCode must identify the same subject.");
        }

        var semesterResult = await ResolveSemesterAsync(request, cancellationToken);
        if (semesterResult.IsFailure)
        {
            return Result.Failure<(Course, Semester, User?, List<int>)>(semesterResult.Error);
        }

        var semester = semesterResult.Value;
        if (!string.IsNullOrWhiteSpace(request.Semester) &&
            (!TryParseSemesterTerm(request.Semester, out var requestedTerm) ||
             semester.Term != requestedTerm ||
             (request.Year.HasValue && semester.Year != request.Year.Value)))
        {
            return Failure(ErrorCodes.ClassValidationError, "semesterId, semester, and year must identify the same academic term.");
        }
        if (semester.Status is SemesterStatus.Completed or SemesterStatus.Archived ||
            (isLecturer && semester.Status != SemesterStatus.Active))
        {
            return Failure(ErrorCodes.ClassValidationError, "Classes can only be created in an academic term that is open for creation.");
        }

        User? lecturer = null;
        var lecturerId = isLecturer ? currentUserId : request.PrimaryLecturerId;
        if (lecturerId.HasValue && lecturerId.Value != Guid.Empty)
        {
            lecturer = await _context.Users
                .AsNoTracking()
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .FirstOrDefaultAsync(user => user.Id == lecturerId.Value, cancellationToken);

            if (lecturer == null || lecturer.Status != UserStatus.Active ||
                !lecturer.UserRoles.Any(userRole =>
                    string.Equals(userRole.Role.Name, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
            {
                return Failure(ErrorCodes.ClassInvalidLecturer, "The lecturer does not exist, is inactive, or does not have LECTURER role.");
            }
        }

        return Result.Success((course, semester, lecturer, targetIndicesResult.Value));
    }

    private static Result<List<int>> BuildTargetIndices(CreateBulkClassesRequest request)
    {
        List<int> indices;
        if (request.ClassIndices is { Count: > 0 })
        {
            if (request.ClassIndices.Count > MaximumBatchSize ||
                request.ClassIndices.Any(index => index is < 1 or > MaximumClassIndex))
            {
                return Result.Failure<List<int>>(
                    new Error(ErrorCodes.ClassValidationError, $"Provide 1-{MaximumBatchSize} class indices between 1 and {MaximumClassIndex}."));
            }

            if (request.ClassIndices.Distinct().Count() != request.ClassIndices.Count)
            {
                return Result.Failure<List<int>>(
                    new Error(ErrorCodes.ClassValidationError, "Class indices must not contain duplicates."));
            }

            indices = request.ClassIndices.Distinct().OrderBy(index => index).ToList();
        }
        else
        {
            if (request.Quantity is < 1 or > MaximumBatchSize ||
                request.StartClassIndex is < 1 or > MaximumClassIndex ||
                request.StartClassIndex + request.Quantity - 1 > MaximumClassIndex)
            {
                return Result.Failure<List<int>>(
                    new Error(ErrorCodes.ClassValidationError, $"Quantity must be 1-{MaximumBatchSize} and generated class indices must be 1-{MaximumClassIndex}."));
            }

            indices = Enumerable.Range(request.StartClassIndex, request.Quantity).ToList();
        }

        return Result.Success(indices);
    }

    private async Task<Result<Semester>> ResolveSemesterAsync(
        CreateBulkClassesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SemesterId != Guid.Empty)
        {
            var byId = await _context.Semesters.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.SemesterId, cancellationToken);
            return byId == null
                ? Result.Failure<Semester>(new Error(ErrorCodes.ClassValidationError, "The specified academic term does not exist."))
                : Result.Success(byId);
        }

        if (!string.IsNullOrWhiteSpace(request.Semester))
        {
            if (!TryParseSemesterTerm(request.Semester, out var term) || !request.Year.HasValue)
            {
                return Result.Failure<Semester>(new Error(ErrorCodes.ClassValidationError, "Semester must be SP, SU, or FA and year is required."));
            }

            var byTerm = await _context.Semesters.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Term == term && item.Year == request.Year.Value, cancellationToken);
            return byTerm == null
                ? Result.Failure<Semester>(new Error(ErrorCodes.ClassValidationError, "The specified academic term does not exist."))
                : Result.Success(byTerm);
        }

        var active = await _context.Semesters.AsNoTracking()
            .Where(item => item.Status == SemesterStatus.Active)
            .OrderByDescending(item => item.Year)
            .ThenBy(item => item.Term)
            .FirstOrDefaultAsync(cancellationToken);
        return active == null
            ? Result.Failure<Semester>(new Error(ErrorCodes.ClassValidationError, "No active academic term is configured."))
            : Result.Success(active);
    }

    private static string BuildClassCode(string courseCode, int classIndex) =>
        $"{courseCode.Trim().ToUpperInvariant()}_{classIndex}";

    private static bool TryParseSemesterTerm(string value, out SemesterTerm term)
    {
        term = value.Trim().ToUpperInvariant() switch
        {
            "SP" or "SPRING" => SemesterTerm.Spring,
            "SU" or "SUMMER" => SemesterTerm.Summer,
            "FA" or "FALL" => SemesterTerm.Fall,
            _ => default
        };
        return value.Trim().ToUpperInvariant() is "SP" or "SPRING" or "SU" or "SUMMER" or "FA" or "FALL";
    }

    private static Result<(Course, Semester, User?, List<int>)> Failure(string code, string message) =>
        Result.Failure<(Course, Semester, User?, List<int>)>(new Error(code, message));
}
