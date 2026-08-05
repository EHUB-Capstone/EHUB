using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
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

namespace EHub.Application.Features.Classes.ImportStudents;

public sealed class PreviewImportStudentsCommandHandler : IPreviewImportStudentsCommandHandler
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private const int MaximumDataRows = 5_000;
    private const int MaximumColumns = 256;
    private const string SpreadsheetMlNamespace = "urn:schemas-microsoft-com:office:spreadsheet";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;

    static PreviewImportStudentsCommandHandler()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public PreviewImportStudentsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ImportStudentsPreviewResponse>> HandleAsync(
        Guid classId,
        IFormFile file,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to import students.");
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
            return Result.Failure<ImportStudentsPreviewResponse>(fileValidation.Error);
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Failure(ErrorCodes.ClassArchived, "Cannot import students to an archived class.");
        }

        if (isLecturer && targetClass.PrimaryLecturerId != currentUserId)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only import students to your assigned class.");
        }

        var parseResult = ParseWorkbook(file);
        if (parseResult.IsFailure)
        {
            return Result.Failure<ImportStudentsPreviewResponse>(parseResult.Error);
        }

        var rows = parseResult.Value;
        await ApplyDatabaseValidationAsync(rows, targetClass, cancellationToken);

        var validRows = rows.Where(row => row.IsValid).ToArray();
        var sessionId = Guid.Empty;

        if (validRows.Length > 0)
        {
            sessionId = Guid.NewGuid();
            _context.ClassImportSessions.Add(new ClassImportSession
            {
                Id = sessionId,
                ClassId = classId,
                UserId = currentUserId,
                ValidRowsJson = JsonSerializer.Serialize(validRows, JsonOptions),
                Status = ClassImportSessionStatus.Available,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.Add(SessionLifetime)
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new ImportStudentsPreviewResponse
        {
            SessionId = sessionId,
            TotalRows = rows.Count,
            ValidRowsCount = validRows.Length,
            ErrorRowsCount = rows.Count - validRows.Length,
            Rows = rows
        });
    }

    internal static Result<List<ImportStudentRowPreviewDto>> ParseWorkbook(IFormFile file)
    {
        try
        {
            using var source = file.OpenReadStream();
            using var content = new MemoryStream();
            source.CopyTo(content);
            content.Position = 0;

            if (LooksLikeXml(content))
            {
                return ParseSpreadsheetMl(content);
            }

            content.Position = 0;
            using var reader = ExcelReaderFactory.CreateReader(content);
            return ParseWorksheet(reader);
        }
        catch
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.InvalidExcelFormat", "Failed to parse the Excel file. Verify that it is a valid .xlsx or .xls workbook."));
        }
    }

    private static bool LooksLikeXml(Stream stream)
    {
        var prefix = new byte[Math.Min(512, checked((int)stream.Length))];
        var read = stream.Read(prefix, 0, prefix.Length);
        stream.Position = 0;

        if (read >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            return LooksLikeUtf16Xml(prefix, read, littleEndian: true);
        }

        if (read >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            return LooksLikeUtf16Xml(prefix, read, littleEndian: false);
        }

        var index = read >= 3 && prefix[0] == 0xEF && prefix[1] == 0xBB && prefix[2] == 0xBF ? 3 : 0;

        while (index < read && char.IsWhiteSpace((char)prefix[index]))
        {
            index++;
        }

        return index < read && prefix[index] == (byte)'<';
    }

    private static bool LooksLikeUtf16Xml(byte[] prefix, int length, bool littleEndian)
    {
        for (var index = 2; index + 1 < length; index += 2)
        {
            var character = littleEndian
                ? (char)(prefix[index] | (prefix[index + 1] << 8))
                : (char)((prefix[index] << 8) | prefix[index + 1]);
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character == '<';
        }

        return false;
    }

    private static Result<List<ImportStudentRowPreviewDto>> ParseSpreadsheetMl(Stream stream)
    {
        stream.Position = 0;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumFileSize * 4,
            IgnoreComments = true
        };

        using var xmlReader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(xmlReader, LoadOptions.None);
        XNamespace spreadsheet = SpreadsheetMlNamespace;
        if (document.Root?.Name != spreadsheet + "Workbook")
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.InvalidExcelFormat", "The XML file is not an Excel 2003 SpreadsheetML workbook."));
        }

        var rowElements = document.Root
            .Elements(spreadsheet + "Worksheet")
            .FirstOrDefault()?
            .Element(spreadsheet + "Table")?
            .Elements(spreadsheet + "Row")
            .Take(MaximumDataRows + 2)
            .ToArray() ?? [];

        if (rowElements.Length == 0)
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.WorksheetEmpty", "The Excel worksheet contains no data."));
        }

        if (rowElements.Length - 1 > MaximumDataRows)
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.TooManyRows", $"An import can contain at most {MaximumDataRows} data rows."));
        }

        var rows = new List<SpreadsheetRow>(rowElements.Length);
        var nextRowNumber = 1;
        foreach (var rowElement in rowElements)
        {
            var explicitRowNumber = ParsePositiveIndex(rowElement.Attribute(spreadsheet + "Index")?.Value);
            var rowNumber = explicitRowNumber ?? nextRowNumber;
            nextRowNumber = rowNumber + 1;
            rows.Add(new SpreadsheetRow(rowNumber, ReadSpreadsheetMlCells(rowElement, spreadsheet)));
        }

        return ParseRows(rows);
    }

    private static IReadOnlyList<string> ReadSpreadsheetMlCells(XElement row, XNamespace spreadsheet)
    {
        var values = new List<string>();
        var columnIndex = 0;

        foreach (var cell in row.Elements(spreadsheet + "Cell"))
        {
            var explicitColumn = ParsePositiveIndex(cell.Attribute(spreadsheet + "Index")?.Value);
            if (explicitColumn.HasValue)
            {
                columnIndex = explicitColumn.Value - 1;
            }

            if (columnIndex >= MaximumColumns)
            {
                break;
            }

            while (values.Count <= columnIndex)
            {
                values.Add(string.Empty);
            }

            values[columnIndex] = cell.Element(spreadsheet + "Data")?.Value.Trim() ?? string.Empty;
            columnIndex++;
        }

        return values;
    }

    private static int? ParsePositiveIndex(string? value) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    private static Result<List<ImportStudentRowPreviewDto>> ParseWorksheet(IExcelDataReader reader)
    {
        if (!reader.Read() || reader.FieldCount == 0)
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.WorksheetEmpty", "The Excel worksheet contains no data."));
        }

        if (reader.FieldCount > MaximumColumns)
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.TooManyColumns", $"An import can contain at most {MaximumColumns} columns."));
        }

        var rows = new List<SpreadsheetRow>
        {
            new(1, Enumerable.Range(0, reader.FieldCount)
                .Select(column => GetCellText(reader.GetValue(column)))
                .ToArray())
        };
        var rowNumber = 1;

        while (reader.Read())
        {
            rowNumber++;
            if (rowNumber - 1 > MaximumDataRows)
            {
                return Result.Failure<List<ImportStudentRowPreviewDto>>(
                    new Error("Classes.TooManyRows", $"An import can contain at most {MaximumDataRows} data rows."));
            }

            rows.Add(new SpreadsheetRow(
                rowNumber,
                Enumerable.Range(0, reader.FieldCount)
                    .Select(column => GetCellText(reader.GetValue(column)))
                    .ToArray()));
        }

        return ParseRows(rows);
    }

    private static Result<List<ImportStudentRowPreviewDto>> ParseRows(IReadOnlyList<SpreadsheetRow> sourceRows)
    {
        if (sourceRows.Count == 0)
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.WorksheetEmpty", "The Excel worksheet contains no data."));
        }

        var columns = FindColumns(sourceRows[0].Cells);
        if (columns.StudentCode < 0 || columns.FullName < 0 || columns.Email < 0)
        {
            return Result.Failure<List<ImportStudentRowPreviewDto>>(
                new Error("Classes.MissingRequiredColumns", "Excel header must contain StudentCode (or RollNumber), FullName, and Email columns. MajorCode is optional for legacy files."));
        }

        var rows = new List<ImportStudentRowPreviewDto>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceRow in sourceRows.Skip(1))
        {
            var rawCode = GetCellText(sourceRow.Cells, columns.StudentCode);
            var rawName = GetCellText(sourceRow.Cells, columns.FullName);
            var rawEmail = GetCellText(sourceRow.Cells, columns.Email);
            var rawMajor = columns.MajorCode >= 0
                ? GetCellText(sourceRow.Cells, columns.MajorCode)
                : MajorCodes.Undeclared;
            if (string.IsNullOrWhiteSpace(rawMajor))
            {
                rawMajor = MajorCodes.Undeclared;
            }

            if (string.IsNullOrWhiteSpace(rawCode) && string.IsNullOrWhiteSpace(rawName) &&
                string.IsNullOrWhiteSpace(rawEmail) && string.IsNullOrWhiteSpace(rawMajor))
            {
                continue;
            }

            var validationError = StudentEnrollmentRules.ValidateAndNormalize(
                rawCode,
                rawName,
                rawEmail,
                rawMajor,
                out var input,
                allowUndeclaredMajor: true);

            if (validationError == null && !seenCodes.Add(input.StudentCode))
            {
                validationError = $"Duplicate Student Code '{input.StudentCode}' found within this Excel file.";
            }
            else if (validationError == null && !seenEmails.Add(input.Email))
            {
                validationError = $"Duplicate Email '{input.Email}' found within this Excel file.";
            }

            rows.Add(new ImportStudentRowPreviewDto
            {
                RowNumber = sourceRow.RowNumber,
                StudentCode = input.StudentCode,
                FullName = input.FullName,
                Email = input.Email,
                MajorCode = input.MajorCode,
                IsValid = validationError == null,
                Status = validationError == null ? "Valid" : "Error",
                ErrorMessage = validationError
            });
        }

        return Result.Success(rows);
    }

    private async Task ApplyDatabaseValidationAsync(
        List<ImportStudentRowPreviewDto> rows,
        Class targetClass,
        CancellationToken cancellationToken)
    {
        var validRows = rows.Where(row => row.IsValid).ToArray();
        if (validRows.Length == 0)
        {
            return;
        }

        var codes = validRows.Select(row => row.StudentCode).ToArray();
        var emails = validRows.Select(row => row.Email).ToArray();
        var profiles = await _context.Students
            .AsNoTracking()
            .Where(student =>
                (student.NormalizedRollNumber != null && codes.Contains(student.NormalizedRollNumber)) ||
                (student.RollNumber != null && codes.Contains(student.RollNumber)) ||
                (student.Email != null && emails.Contains(student.Email.ToLower())))
            .ToListAsync(cancellationToken);

        var profilesByCode = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.NormalizedRollNumber ?? student.RollNumber))
            .GroupBy(student => student.NormalizedRollNumber ?? student.RollNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var profilesByEmail = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.Email))
            .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var profileIds = profiles.Select(student => student.Id).ToArray();

        var enrollments = profileIds.Length == 0
            ? []
            : await _context.ClassStudents
                .AsNoTracking()
                .Include(enrollment => enrollment.Class)
                .Where(enrollment =>
                    profileIds.Contains(enrollment.StudentId) &&
                    (enrollment.ClassId == targetClass.Id ||
                     (enrollment.CountsTowardCourseSemesterLimit &&
                      enrollment.SemesterId == targetClass.SemesterId &&
                      enrollment.CourseId == targetClass.CourseId)))
                .ToListAsync(cancellationToken);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!row.IsValid)
            {
                continue;
            }

            profilesByCode.TryGetValue(row.StudentCode, out var codeProfiles);
            var profileByCode = codeProfiles?.Length == 1 ? codeProfiles[0] : null;
            profilesByEmail.TryGetValue(row.Email, out var emailProfiles);
            var profileByEmail = emailProfiles?.Length == 1 ? emailProfiles[0] : null;
            string? error = null;

            if ((codeProfiles?.Length ?? 0) > 1 ||
                (emailProfiles?.Length ?? 0) > 1 ||
                (profileByCode != null && profileByEmail != null && profileByCode.Id != profileByEmail.Id) ||
                (profileByCode != null && !string.Equals(profileByCode.Email, row.Email, StringComparison.OrdinalIgnoreCase)) ||
                (profileByEmail != null &&
                 !string.Equals(profileByEmail.NormalizedRollNumber ?? profileByEmail.RollNumber, row.StudentCode, StringComparison.OrdinalIgnoreCase)))
            {
                error = "Student code and email do not identify one unique student profile.";
            }
            else
            {
                var profile = profileByCode ?? profileByEmail;
                var currentEnrollment = profile == null
                    ? null
                    : enrollments.FirstOrDefault(enrollment =>
                        enrollment.StudentId == profile.Id && enrollment.ClassId == targetClass.Id);
                if (currentEnrollment != null)
                {
                    error = currentEnrollment.EnrollmentStatus == EnrollmentStatus.Dropped
                        ? $"Student '{row.StudentCode}' has a dropped enrollment. Use the explicit re-enroll action."
                        : $"Student '{row.StudentCode}' already has an enrollment in this class.";
                }
                else
                {
                    var conflict = profile == null
                        ? null
                        : enrollments.FirstOrDefault(enrollment =>
                            enrollment.StudentId == profile.Id && enrollment.CountsTowardCourseSemesterLimit);
                    if (conflict != null)
                    {
                        error = $"Student '{row.StudentCode}' is already enrolled in class '{conflict.Class.ClassCode}' for the same course and semester.";
                    }
                }
            }

            if (error != null)
            {
                rows[index] = Invalid(row, error);
            }
        }
    }

    private static ImportStudentRowPreviewDto Invalid(ImportStudentRowPreviewDto row, string error) => new()
    {
        RowNumber = row.RowNumber,
        StudentCode = row.StudentCode,
        FullName = row.FullName,
        Email = row.Email,
        MajorCode = row.MajorCode,
        IsValid = false,
        Status = "Error",
        ErrorMessage = error
    };

    private static string GetCellText(object? value) =>
        Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static string GetCellText(IReadOnlyList<string> values, int columnIndex) =>
        columnIndex >= 0 && columnIndex < values.Count ? values[columnIndex].Trim() : string.Empty;

    private static (int StudentCode, int FullName, int Email, int MajorCode) FindColumns(
        IReadOnlyList<string> header)
    {
        var studentCode = -1;
        var fullName = -1;
        var email = -1;
        var majorCode = -1;

        for (var column = 0; column < header.Count; column++)
        {
            var value = header[column].Replace(" ", string.Empty).ToLowerInvariant();
            if (value is "studentcode" or "rollnumber" or "mssv") studentCode = column;
            else if (value is "fullname" or "name") fullName = column;
            else if (value == "email") email = column;
            else if (value is "majorcode" or "major") majorCode = column;
        }

        return (studentCode, fullName, email, majorCode);
    }

    private sealed record SpreadsheetRow(int RowNumber, IReadOnlyList<string> Cells);

    private static Result<ImportStudentsPreviewResponse> Failure(string code, string message) =>
        Result.Failure<ImportStudentsPreviewResponse>(new Error(code, message));
}
