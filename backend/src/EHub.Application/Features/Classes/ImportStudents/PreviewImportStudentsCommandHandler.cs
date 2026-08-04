using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ImportStudents;

public sealed class PreviewImportStudentsCommandHandler : IPreviewImportStudentsCommandHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IImportSessionStore _sessionStore;

    public PreviewImportStudentsCommandHandler(
        IApplicationDbContext context,
        IImportSessionStore sessionStore)
    {
        _context = context;
        _sessionStore = sessionStore;
    }

    public async Task<Result<ImportStudentsPreviewResponse>> HandleAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);

        // Safety hotfix: keep import Admin-only until the durable session/transaction redesign is complete.
        if (!isAdmin)
        {
            return Result.Failure<ImportStudentsPreviewResponse>(
                new Error(ErrorCodes.ClassAccessDenied, "Only Admin can import students during the safety hardening period."));
        }

        if (file == null || file.Length == 0)
        {
            return Result.Failure<ImportStudentsPreviewResponse>(
                new Error("Classes.FileEmpty", "The uploaded Excel file is empty."));
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return Result.Failure<ImportStudentsPreviewResponse>(
                new Error("Classes.FileTooLarge", "Excel file size exceeds the 10 MB limit."));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
        {
            return Result.Failure<ImportStudentsPreviewResponse>(
                new Error("Classes.InvalidFileType", "Only OpenXML Excel files (.xlsx) are allowed."));
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(c => c.ClassLecturers)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ImportStudentsPreviewResponse>(
                new Error("Classes.NotFound", "The requested class was not found."));
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Result.Failure<ImportStudentsPreviewResponse>(
                new Error("Classes.ClassArchived", "Cannot import students to an archived class."));
        }

        // Fetch existing enrollments in this class & active enrollments across same subject/semester
        var existingInClassStudents = await _context.ClassStudents
            .AsNoTracking()
            .Include(cs => cs.Student)
            .Where(cs => cs.ClassId == classId && cs.EnrollmentStatus == EnrollmentStatus.Active)
            .Select(cs => cs.Student.NormalizedRollNumber ?? cs.Student.RollNumber)
            .Where(r => r != null)
            .ToListAsync(cancellationToken);

        var existingInClassSet = new HashSet<string>(existingInClassStudents!, StringComparer.OrdinalIgnoreCase);

        var existingCrossClassEnrollments = await _context.ClassStudents
            .AsNoTracking()
            .Include(cs => cs.Student)
            .Include(cs => cs.Class)
            .Where(cs => cs.ClassId != classId &&
                         cs.Class.CourseId == targetClass.CourseId &&
                         cs.Class.SemesterId == targetClass.SemesterId &&
                         cs.Class.Status == ClassStatus.Active &&
                         cs.EnrollmentStatus == EnrollmentStatus.Active)
            .Select(cs => new { RollNumber = cs.Student.NormalizedRollNumber ?? cs.Student.RollNumber, ClassCode = cs.Class.ClassCode })
            .Where(x => x.RollNumber != null)
            .ToListAsync(cancellationToken);

        var crossClassConflictDict = existingCrossClassEnrollments
            .GroupBy(x => x.RollNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ClassCode, StringComparer.OrdinalIgnoreCase);

        var rowPreviews = new List<ImportStudentRowPreviewDto>();
        var seenFileRollNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        XLWorkbook workbook;
        try
        {
            using var stream = file.OpenReadStream();
            workbook = new XLWorkbook(stream);
        }
        catch
        {
            return Result.Failure<ImportStudentsPreviewResponse>(
                new Error("Classes.InvalidExcelFormat", "Failed to parse Excel file. Please convert or save the file as OpenXML (.xlsx) format."));
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                return Result.Failure<ImportStudentsPreviewResponse>(
                    new Error("Classes.WorksheetEmpty", "The Excel file contains no worksheets."));
            }

            var range = worksheet.RangeUsed();
            if (range == null)
            {
                return Result.Failure<ImportStudentsPreviewResponse>(
                    new Error("Classes.WorksheetEmpty", "The Excel worksheet contains no data."));
            }

            var headerRow = range.Row(1);
            int studentCodeCol = -1;
            int fullNameCol = -1;
            int emailCol = -1;
            int majorCol = -1;

            for (int col = 1; col <= range.ColumnCount(); col++)
            {
                var val = headerRow.Cell(col).GetString().Trim().ToLowerInvariant();
                if (val.Contains("studentcode") || val.Contains("rollnumber") || val.Contains("mã sinh viên") || val.Contains("mssv"))
                {
                    studentCodeCol = col;
                }
                else if (val.Contains("fullname") || val.Contains("họ tên") || val.Contains("name"))
                {
                    fullNameCol = col;
                }
                else if (val.Contains("email"))
                {
                    emailCol = col;
                }
                else if (val.Contains("major") || val.Contains("ngành"))
                {
                    majorCol = col;
                }
            }

            if (studentCodeCol == -1 || fullNameCol == -1 || emailCol == -1)
            {
                return Result.Failure<ImportStudentsPreviewResponse>(
                    new Error("Classes.MissingRequiredColumns", "Excel header must contain StudentCode (or RollNumber), FullName, and Email columns."));
            }

            int rowNumber = 1;
            foreach (var row in range.Rows().Skip(1))
            {
                rowNumber++;
                var studentCode = row.Cell(studentCodeCol).GetString().Trim().ToUpperInvariant();
                var fullName = row.Cell(fullNameCol).GetString().Trim();
                var email = row.Cell(emailCol).GetString().Trim().ToLowerInvariant();
                var majorCode = majorCol != -1 ? row.Cell(majorCol).GetString().Trim().ToUpperInvariant() : "BIT_SE";

                if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(email))
                {
                    continue; // skip blank row
                }

                string? errorMessage = null;
                bool isValid = true;

                if (string.IsNullOrWhiteSpace(studentCode))
                {
                    isValid = false;
                    errorMessage = "Student Code is required.";
                }
                else if (string.IsNullOrWhiteSpace(fullName))
                {
                    isValid = false;
                    errorMessage = "Full Name is required.";
                }
                else if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email, out _))
                {
                    isValid = false;
                    errorMessage = $"Email '{email}' is invalid.";
                }
                else if (!MajorCodes.IsValid(majorCode))
                {
                    isValid = false;
                    errorMessage = $"Major code '{majorCode}' is invalid.";
                }
                else if (seenFileRollNumbers.Contains(studentCode))
                {
                    isValid = false;
                    errorMessage = $"Duplicate Student Code '{studentCode}' found within this Excel file.";
                }
                else if (existingInClassSet.Contains(studentCode))
                {
                    isValid = false;
                    errorMessage = $"Student '{studentCode}' is already actively enrolled in this class.";
                }
                else if (crossClassConflictDict.TryGetValue(studentCode, out var conflictClassCode))
                {
                    isValid = false;
                    errorMessage = $"Student '{studentCode}' is already enrolled in active class '{conflictClassCode}' for the same subject and semester.";
                }

                if (isValid)
                {
                    seenFileRollNumbers.Add(studentCode);
                }

                rowPreviews.Add(new ImportStudentRowPreviewDto
                {
                    RowNumber = rowNumber,
                    StudentCode = studentCode,
                    FullName = fullName,
                    Email = email,
                    MajorCode = string.IsNullOrWhiteSpace(majorCode) ? "BIT_SE" : majorCode,
                    IsValid = isValid,
                    Status = isValid ? "Valid" : "Error",
                    ErrorMessage = errorMessage
                });
            }
        }

        var validRows = rowPreviews.Where(r => r.IsValid).ToList();
        var sessionId = Guid.NewGuid();

        _sessionStore.SaveSession(sessionId, classId, currentUserId, validRows);

        var response = new ImportStudentsPreviewResponse
        {
            SessionId = sessionId,
            TotalRows = rowPreviews.Count,
            ValidRowsCount = validRows.Count,
            ErrorRowsCount = rowPreviews.Count - validRows.Count,
            Rows = rowPreviews
        };

        return Result.Success(response);
    }
}
