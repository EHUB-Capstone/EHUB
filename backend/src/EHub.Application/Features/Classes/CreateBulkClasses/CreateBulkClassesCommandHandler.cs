using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
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
        var validationResult = await ValidateCommonAsync(request, currentUserId, currentUserRole, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result.Failure<BulkClassPreviewResponse>(validationResult.Error);
        }

        var (course, semester, lecturerUser, targetIndices) = validationResult.Value;

        var existingCodes = await _context.Classes
            .Where(c => c.SemesterId == semester.Id)
            .Select(c => c.ClassCode)
            .ToListAsync(cancellationToken);

        var existingIndexes = await _context.Classes
            .Where(c => c.SemesterId == semester.Id && c.CourseId == course.Id)
            .Select(c => c.ClassIndex)
            .ToListAsync(cancellationToken);

        var existingCodesSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var existingIndexesSet = new HashSet<int>(existingIndexes);

        var items = new List<BulkClassPreviewItem>();
        var validCount = 0;
        var invalidCount = 0;

        foreach (var currentIndex in targetIndices)
        {
            var classCode = $"{course.Code.Trim().ToUpperInvariant()}_{currentIndex}";

            string? errorMessage = null;
            bool isValid = true;

            if (existingCodesSet.Contains(classCode))
            {
                isValid = false;
                errorMessage = $"Class code '{classCode}' already exists in this semester.";
            }
            else if (existingIndexesSet.Contains(currentIndex))
            {
                isValid = false;
                errorMessage = $"Class index {currentIndex} already exists for this subject.";
            }

            if (isValid)
            {
                validCount++;
            }
            else
            {
                invalidCount++;
            }

            items.Add(new BulkClassPreviewItem
            {
                ClassCode = classCode,
                ClassIndex = currentIndex,
                SubjectCode = course.Code,
                SemesterCode = semester.Code,
                PrimaryLecturerName = lecturerUser?.FullName,
                IsValid = isValid,
                ErrorMessage = errorMessage
            });
        }

        var response = new BulkClassPreviewResponse
        {
            Items = items,
            TotalCount = targetIndices.Count,
            ValidCount = validCount,
            InvalidCount = invalidCount
        };

        return Result.Success(response);
    }

    public async Task<Result<IReadOnlyCollection<ClassResponse>>> CommitAsync(
        CreateBulkClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateCommonAsync(request, currentUserId, currentUserRole, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<ClassResponse>>(validationResult.Error);
        }

        var (course, semester, lecturerUser, targetIndices) = validationResult.Value;

        // Preview validation check
        var previewResult = await PreviewAsync(request, currentUserId, currentUserRole, cancellationToken);
        if (previewResult.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<ClassResponse>>(previewResult.Error);
        }

        if (previewResult.Value.InvalidCount > 0)
        {
            var firstError = previewResult.Value.Items.FirstOrDefault(i => !i.IsValid)?.ErrorMessage ?? "Bulk creation contains invalid classes.";
            return Result.Failure<IReadOnlyCollection<ClassResponse>>(
                new Error("Classes.BulkCreateInvalid", $"Batch creation failed: {firstError}"));
        }

        try
        {
            var createdClasses = new List<Class>();
            var responses = new List<ClassResponse>();

            foreach (var currentIndex in targetIndices)
            {
                var classCode = $"{course.Code.Trim().ToUpperInvariant()}_{currentIndex}";

                var newClass = new Class
                {
                    ClassCode = classCode,
                    ClassIndex = currentIndex,
                    SemesterId = semester.Id,
                    CourseId = course.Id,
                    PrimaryLecturerId = lecturerUser?.Id,
                    Status = ClassStatus.Active,
                    CreatedById = currentUserId
                };

                createdClasses.Add(newClass);
                _context.Classes.Add(newClass);

                if (lecturerUser != null)
                {
                    _context.ClassLecturers.Add(new ClassLecturer
                    {
                        ClassId = newClass.Id,
                        LecturerId = lecturerUser.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                }

                responses.Add(new ClassResponse
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
                    PrimaryLecturerId = lecturerUser?.Id,
                    PrimaryLecturerName = lecturerUser?.FullName,
                    PrimaryLecturerEmail = lecturerUser?.Email,
                    Status = newClass.Status.ToString(),
                    StudentCount = 0,
                    TeamCount = 0,
                    CreatedAtUtc = newClass.CreatedAt
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<IReadOnlyCollection<ClassResponse>>(responses);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyCollection<ClassResponse>>(
                new Error("Classes.BulkCreateFailed", $"Bulk class creation failed: {ex.Message}"));
        }
    }

    private async Task<Result<(Course Course, Semester Semester, User? LecturerUser, List<int> TargetIndices)>> ValidateCommonAsync(
        CreateBulkClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<(Course, Semester, User?, List<int>)>(
                new Error("Classes.AccessDenied", "You do not have permission to bulk create classes."));
        }

        // Calculate quantity & target indices
        var targetIndices = new List<int>();

        if (request.ClassIndices != null && request.ClassIndices.Count > 0)
        {
            targetIndices = request.ClassIndices.Where(i => i > 0).Distinct().OrderBy(i => i).ToList();
        }
        else
        {
            var quantity = request.Count ?? request.Quantity;
            if (quantity is < 1 or > 100)
            {
                return Result.Failure<(Course, Semester, User?, List<int>)>(
                    new Error("Classes.InvalidQuantity", "Quantity must be between 1 and 100."));
            }

            var startClassIndex = request.StartClassIndex <= 0 ? 1 : request.StartClassIndex;
            for (int i = 0; i < quantity; i++)
            {
                targetIndices.Add(startClassIndex + i);
            }
        }

        if (targetIndices.Count == 0)
        {
            return Result.Failure<(Course, Semester, User?, List<int>)>(
                new Error("Classes.InvalidClassIndices", "No valid class indices specified."));
        }

        // Resolve Course
        Course? course = null;
        if (request.CourseId != Guid.Empty)
        {
            course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.SubjectCode))
        {
            var code = request.SubjectCode.Trim().ToUpperInvariant();
            course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
        }

        if (course == null || course.Status != CourseStatus.Active)
        {
            return Result.Failure<(Course, Semester, User?, List<int>)>(
                new Error("Classes.SubjectNotFoundOrInactive", "The specified subject does not exist or is inactive."));
        }

        // Resolve Semester
        Semester? semester = null;
        if (request.SemesterId != Guid.Empty)
        {
            semester = await _context.Semesters.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SemesterId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.Semester))
        {
            var term = ToSemesterTerm(request.Semester);
            var year = request.Year ?? DateTime.UtcNow.Year;

            semester = await _context.Semesters.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Term == term && s.Year == year, cancellationToken);

            if (semester == null)
            {
                // Fallback to active semester
                semester = await _context.Semesters.AsNoTracking()
                    .Where(s => s.Status == SemesterStatus.Active)
                    .OrderByDescending(s => s.Year)
                    .ThenBy(s => s.Term)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }
        else
        {
            // Default active semester
            semester = await _context.Semesters.AsNoTracking()
                .Where(s => s.Status == SemesterStatus.Active)
                .OrderByDescending(s => s.Year)
                .ThenBy(s => s.Term)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (semester == null)
        {
            return Result.Failure<(Course, Semester, User?, List<int>)>(
                new Error("Classes.SemesterNotFound", "The specified academic term does not exist."));
        }

        // Resolve Primary Lecturer
        User? lecturerUser = null;

        if (isLecturer)
        {
            lecturerUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);
        }
        else if (isAdmin)
        {
            Guid? targetLecturerId = request.PrimaryLecturerId;
            if (!targetLecturerId.HasValue && request.LecturerIds != null && request.LecturerIds.Count > 0)
            {
                targetLecturerId = request.LecturerIds.FirstOrDefault();
            }

            if (targetLecturerId.HasValue && targetLecturerId.Value != Guid.Empty)
            {
                lecturerUser = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == targetLecturerId.Value, cancellationToken);

                if (lecturerUser == null || lecturerUser.Status != UserStatus.Active ||
                    !lecturerUser.UserRoles.Any(ur => string.Equals(ur.Role.Name, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
                {
                    return Result.Failure<(Course, Semester, User?, List<int>)>(
                        new Error("Classes.InvalidLecturer", "The specified lecturer does not exist, is inactive, or does not have LECTURER role."));
                }
            }
        }

        return Result.Success((course, semester, lecturerUser, targetIndices));
    }

    private static SemesterTerm ToSemesterTerm(string value) => value.Trim().ToUpperInvariant() switch
    {
        "SP" or "SPRING" => SemesterTerm.Spring,
        "SU" or "SUMMER" => SemesterTerm.Summer,
        "FA" or "FALL" => SemesterTerm.Fall,
        _ => SemesterTerm.Spring
    };
}
