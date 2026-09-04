using System.Globalization;
using System.Text.Json;
using ExcelDataReader;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.VerifyClassMajors;

public sealed class VerifyClassMajorsCommandHandler : IVerifyClassMajorsCommandHandler
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private const int MaximumRows = 5_000;
    private readonly IApplicationDbContext _context;

    static VerifyClassMajorsCommandHandler()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public VerifyClassMajorsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<VerifyClassMajorsResponse>> HandleAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!ClassAuthorizationRules.IsStaff(currentUserRole))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can verify enrollment majors.");
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (!ClassAuthorizationRules.CanManageClass(
                targetClass.PrimaryLecturerId,
                currentUserId,
                currentUserRole))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only verify enrollment majors for classes assigned to you.");
        }

        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null)
        {
            return Failure(mutationError.Code, mutationError.Message);
        }

        if (file == null || file.Length == 0)
        {
            return Failure("Classes.FileEmpty", "The uploaded Excel file is empty.");
        }

        if (file.Length > MaximumFileSize)
        {
            return Failure("Classes.FileTooLarge", "Excel file size exceeds the 10 MB limit.");
        }

        var fileValidation = ExcelWorkbookSecurity.Validate(file);
        if (fileValidation.IsFailure)
        {
            return Result.Failure<VerifyClassMajorsResponse>(fileValidation.Error);
        }

        var parsed = Parse(file);
        if (parsed.IsFailure)
        {
            return Result.Failure<VerifyClassMajorsResponse>(parsed.Error);
        }

        var sourceRows = parsed.Value;
        var duplicateCodes = sourceRows
            .Where(row => !string.IsNullOrWhiteSpace(row.StudentCode))
            .GroupBy(row => row.StudentCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceByCode = sourceRows
            .Where(row => !string.IsNullOrWhiteSpace(row.StudentCode) && !duplicateCodes.Contains(row.StudentCode))
            .ToDictionary(row => row.StudentCode, StringComparer.OrdinalIgnoreCase);

        var enrollments = await _context.ClassStudents
            .Include(enrollment => enrollment.Student)
            .Where(enrollment =>
                enrollment.ClassId == classId &&
                enrollment.EnrollmentStatus == EnrollmentStatus.Active)
            .OrderBy(enrollment => enrollment.Student.RollNumber)
            .ToListAsync(cancellationToken);

        var matched = new List<MajorVerificationRowDto>();
        var mismatched = new List<MajorVerificationRowDto>();
        var missing = new List<MajorVerificationRowDto>();
        var notFound = new List<MajorVerificationRowDto>();
        var rosterCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verifiedAt = DateTime.UtcNow;

        foreach (var enrollment in enrollments)
        {
            var studentCode = (enrollment.Student.NormalizedRollNumber ?? enrollment.Student.RollNumber ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
            rosterCodes.Add(studentCode);

            MajorSourceRow? source = null;
            EnrollmentMajorVerificationStatus status;
            string? message = null;
            if (duplicateCodes.Contains(studentCode))
            {
                status = EnrollmentMajorVerificationStatus.NotFound;
                message = "The verification file contains duplicate rows for this student code.";
            }
            else if (!sourceByCode.TryGetValue(studentCode, out source))
            {
                status = EnrollmentMajorVerificationStatus.NotFound;
                message = "The active enrollment was not found in the verification file.";
            }
            else if (string.IsNullOrWhiteSpace(source.MajorCode) || !MajorCodes.IsValid(source.MajorCode))
            {
                status = EnrollmentMajorVerificationStatus.Missing;
                message = string.IsNullOrWhiteSpace(source.MajorCode)
                    ? "Major is missing in the verification file."
                    : $"Major code '{source.MajorCode}' is not recognized.";
            }
            else if (string.Equals(source.MajorCode, enrollment.MajorCodeAtEnrollment, StringComparison.OrdinalIgnoreCase))
            {
                status = EnrollmentMajorVerificationStatus.Matched;
            }
            else
            {
                status = EnrollmentMajorVerificationStatus.Mismatched;
                message = "The imported major differs from the enrollment snapshot.";
            }

            enrollment.MajorVerificationStatus = status;
            enrollment.MajorVerifiedAtUtc = verifiedAt;
            enrollment.MajorVerifiedByUserId = currentUserId;
            enrollment.UpdatedAt = verifiedAt;

            AddToBucket(new MajorVerificationRowDto
            {
                RowNumber = source?.RowNumber,
                StudentId = enrollment.StudentId,
                RollNumber = enrollment.Student.RollNumber ?? studentCode,
                FullName = enrollment.Student.FullName,
                Email = enrollment.Student.Email ?? string.Empty,
                MajorInFile = source?.MajorCode,
                MajorInDb = enrollment.MajorCodeAtEnrollment,
                Status = status.ToString(),
                Message = message
            }, status, matched, mismatched, missing, notFound);
        }

        foreach (var source in sourceRows.Where(row =>
                     string.IsNullOrWhiteSpace(row.StudentCode) ||
                     !rosterCodes.Contains(row.StudentCode)))
        {
            notFound.Add(new MajorVerificationRowDto
            {
                RowNumber = source.RowNumber,
                RollNumber = source.StudentCode,
                MajorInFile = source.MajorCode,
                Status = EnrollmentMajorVerificationStatus.NotFound.ToString(),
                Message = string.IsNullOrWhiteSpace(source.StudentCode)
                    ? "Student code is missing in this verification row."
                    : "The student code was not found in the active class roster."
            });
        }

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = classId,
            Action = "ENROLLMENT_MAJORS_VERIFIED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = verifiedAt,
            DetailsJson = JsonSerializer.Serialize(new
            {
                FileName = Path.GetFileName(file.FileName),
                MatchedCount = matched.Count,
                MismatchedCount = mismatched.Count,
                MissingCount = missing.Count,
                NotFoundCount = notFound.Count
            })
        });
        ClassOutbox.Enqueue(_context, "Class.EnrollmentMajorsVerified.v1", classId, new
        {
            MatchedCount = matched.Count,
            MismatchedCount = mismatched.Count,
            MissingCount = missing.Count,
            NotFoundCount = notFound.Count
        }, verifiedAt);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The roster changed concurrently. Refresh and verify the file again.");
        }

        return Result.Success(new VerifyClassMajorsResponse
        {
            Matched = matched,
            Mismatched = mismatched,
            Missing = missing,
            NotFound = notFound
        });
    }

    private static Result<List<MajorSourceRow>> Parse(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);
            if (!reader.Read())
            {
                return ParseFailure("The Excel worksheet contains no data.");
            }

            var studentCodeColumn = -1;
            var majorCodeColumn = -1;
            for (var column = 0; column < reader.FieldCount; column++)
            {
                var header = NormalizeHeader(reader.GetValue(column));
                if (header is "studentcode" or "rollnumber" or "mssv" or "id") studentCodeColumn = column;
                if (header is "majorcode" or "major" or "chuyênngành" or "chuyennganh") majorCodeColumn = column;
            }

            if (studentCodeColumn < 0 || majorCodeColumn < 0)
            {
                return ParseFailure("Excel header must contain StudentCode and MajorCode columns.");
            }

            var rows = new List<MajorSourceRow>();
            var rowNumber = 1;
            while (reader.Read())
            {
                rowNumber++;
                if (rows.Count >= MaximumRows)
                {
                    return ParseFailure($"A verification file can contain at most {MaximumRows} data rows.");
                }

                var studentCode = GetText(reader.GetValue(studentCodeColumn)).ToUpperInvariant();
                var majorCode = GetText(reader.GetValue(majorCodeColumn)).ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(majorCode))
                {
                    continue;
                }

                rows.Add(new MajorSourceRow(rowNumber, studentCode, majorCode));
            }

            return Result.Success(rows);
        }
        catch
        {
            return ParseFailure("Failed to parse the Excel verification file.");
        }
    }

    private static string NormalizeHeader(object? value) =>
        GetText(value).Replace(" ", string.Empty).ToLowerInvariant();

    private static string GetText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static void AddToBucket(
        MajorVerificationRowDto row,
        EnrollmentMajorVerificationStatus status,
        ICollection<MajorVerificationRowDto> matched,
        ICollection<MajorVerificationRowDto> mismatched,
        ICollection<MajorVerificationRowDto> missing,
        ICollection<MajorVerificationRowDto> notFound)
    {
        switch (status)
        {
            case EnrollmentMajorVerificationStatus.Matched: matched.Add(row); break;
            case EnrollmentMajorVerificationStatus.Mismatched: mismatched.Add(row); break;
            case EnrollmentMajorVerificationStatus.Missing: missing.Add(row); break;
            default: notFound.Add(row); break;
        }
    }

    private static Result<List<MajorSourceRow>> ParseFailure(string message) =>
        Result.Failure<List<MajorSourceRow>>(new Error("Classes.InvalidExcelFormat", message));

    private static Result<VerifyClassMajorsResponse> Failure(string code, string message) =>
        Result.Failure<VerifyClassMajorsResponse>(new Error(code, message));

    private sealed record MajorSourceRow(int RowNumber, string StudentCode, string MajorCode);
}
