using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ImportSemesterGroups;

public sealed class ImportSemesterGroupsCommandHandler : IImportSemesterGroupsCommandHandler
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private readonly IApplicationDbContext _context;

    public ImportSemesterGroupsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Result<SemesterGroupImportResponse>> PreviewAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(classId, file, currentUserId, currentUserRole, previewOnly: true, cancellationToken);

    public Task<Result<SemesterGroupImportResponse>> ImportAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(classId, file, currentUserId, currentUserRole, previewOnly: false, cancellationToken);

    private async Task<Result<SemesterGroupImportResponse>> ProcessAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        if (!ClassAuthorizationRules.IsStaff(currentUserRole))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can import semester groups.");
        }

        var classInfo = await _context.Classes
            .AsNoTracking()
            .Where(item => item.Id == classId)
            .Select(item => new
            {
                item.PrimaryLecturerId,
                item.Status,
                SemesterCode = item.Semester.Code
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (classInfo == null) return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");

        if (!ClassAuthorizationRules.CanManageClass(
                classInfo.PrimaryLecturerId,
                currentUserId,
                currentUserRole))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only import semester groups for classes assigned to you.");
        }

        var mutationError = ClassStateRules.GetMutationError(classInfo.Status);
        if (mutationError != null) return Failure(mutationError.Code, mutationError.Message);

        if (file == null || file.Length == 0) return Failure("Classes.FileEmpty", "The uploaded Excel file is empty.");
        if (file.Length > MaximumFileSize) return Failure("Classes.FileTooLarge", "Excel file size exceeds the 10 MB limit.");

        var fileValidation = ExcelWorkbookSecurity.Validate(file);
        if (fileValidation.IsFailure) return Result.Failure<SemesterGroupImportResponse>(fileValidation.Error);

        var expectedColumnName = SemesterGroupColumn.GetHeader(classInfo.SemesterCode);
        var parsed = SemesterGroupImportWorkbookParser.Parse(file, expectedColumnName);
        if (parsed.IsFailure) return Result.Failure<SemesterGroupImportResponse>(parsed.Error);

        var sourceRows = parsed.Value;
        var duplicateRollNumbers = sourceRows
            .Where(row => !string.IsNullOrWhiteSpace(row.RollNumber))
            .GroupBy(row => row.RollNumber, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enrollments = await _context.ClassStudents
            .Include(item => item.Student)
            .Where(item => item.ClassId == classId && item.EnrollmentStatus == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
        var enrollmentsByRollNumber = enrollments
            .GroupBy(item => NormalizeRollNumber(item.Student.NormalizedRollNumber ?? item.Student.RollNumber))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

        var previewRows = new List<SemesterGroupImportRowDto>(sourceRows.Count);
        foreach (var sourceRow in sourceRows)
        {
            var normalizedRollNumber = NormalizeRollNumber(sourceRow.RollNumber);
            ClassStudent? enrollment = null;
            string status;
            string? message = null;
            var isValid = true;

            if (string.IsNullOrWhiteSpace(normalizedRollNumber))
            {
                status = "Invalid";
                message = "RollNumber is required.";
                isValid = false;
            }
            else if (duplicateRollNumbers.Contains(normalizedRollNumber))
            {
                status = "Duplicate";
                message = "RollNumber appears more than once in the file.";
                isValid = false;
            }
            else if (string.IsNullOrWhiteSpace(sourceRow.GroupName))
            {
                status = "Invalid";
                message = $"{expectedColumnName} is required.";
                isValid = false;
            }
            else if (sourceRow.GroupName.Length > SemesterGroupImportWorkbookParser.MaximumGroupNameLength)
            {
                status = "Invalid";
                message = $"{expectedColumnName} cannot exceed {SemesterGroupImportWorkbookParser.MaximumGroupNameLength} characters.";
                isValid = false;
            }
            else if (!enrollmentsByRollNumber.TryGetValue(normalizedRollNumber, out enrollment))
            {
                status = "NotFound";
                message = "RollNumber was not found in the active class roster.";
                isValid = false;
            }
            else if (string.Equals(enrollment.SemesterGroupName, sourceRow.GroupName, StringComparison.Ordinal))
            {
                status = "Unchanged";
            }
            else
            {
                status = "Changed";
            }

            previewRows.Add(new SemesterGroupImportRowDto
            {
                RowNumber = sourceRow.RowNumber,
                StudentId = enrollment?.StudentId,
                RollNumber = sourceRow.RollNumber,
                FullName = enrollment?.Student.FullName ?? string.Empty,
                CurrentGroupName = enrollment?.SemesterGroupName,
                ImportedGroupName = string.IsNullOrWhiteSpace(sourceRow.GroupName) ? null : sourceRow.GroupName,
                Status = status,
                IsValid = isValid,
                Message = message
            });
        }

        var errorRowsCount = previewRows.Count(row => !row.IsValid);
        var changedRows = previewRows.Where(row => row.IsValid && row.Status == "Changed").ToList();
        var unchangedRowsCount = previewRows.Count(row => row.IsValid && row.Status == "Unchanged");
        if (!previewOnly && errorRowsCount > 0)
        {
            return Failure(ErrorCodes.ClassValidationError,
                "The file contains invalid, duplicate, or unmatched rows. No semester groups were changed.");
        }

        if (!previewOnly && changedRows.Count == 0 && unchangedRowsCount == 0)
        {
            return Failure(ErrorCodes.ClassValidationError, "No RollNumber in the file matches an active student in this class.");
        }

        var updatedCount = 0;
        if (!previewOnly)
        {
            var changedAtUtc = DateTime.UtcNow;
            foreach (var row in changedRows)
            {
                var enrollment = enrollmentsByRollNumber[NormalizeRollNumber(row.RollNumber)];
                enrollment.SemesterGroupName = row.ImportedGroupName;
                enrollment.UpdatedAt = changedAtUtc;
                updatedCount++;
            }

            _context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = classId,
                Action = "SEMESTER_GROUPS_IMPORTED",
                PerformedByUserId = currentUserId,
                OccurredAtUtc = changedAtUtc,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    FileName = Path.GetFileName(file.FileName),
                    ExpectedColumnName = expectedColumnName,
                    UpdatedCount = updatedCount,
                    UnchangedCount = unchangedRowsCount
                })
            });
            ClassOutbox.Enqueue(_context, "Class.SemesterGroupsImported.v1", classId, new
            {
                ExpectedColumnName = expectedColumnName,
                UpdatedCount = updatedCount,
                UnchangedCount = unchangedRowsCount
            }, changedAtUtc);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure(ErrorCodes.ClassConcurrencyConflict,
                    "The roster changed concurrently. Refresh and preview the file again.");
            }
        }

        return Result.Success(new SemesterGroupImportResponse
        {
            ExpectedColumnName = expectedColumnName,
            TotalRows = previewRows.Count,
            ChangedRowsCount = changedRows.Count,
            UnchangedRowsCount = unchangedRowsCount,
            ErrorRowsCount = errorRowsCount,
            UpdatedCount = updatedCount,
            Rows = previewRows
        });
    }

    private static string NormalizeRollNumber(string? rollNumber) =>
        rollNumber?.Trim().ToUpperInvariant() ?? string.Empty;

    private static Result<SemesterGroupImportResponse> Failure(string code, string message) =>
        Result.Failure<SemesterGroupImportResponse>(new Error(code, message));
}
