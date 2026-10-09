using System.Globalization;
using System.Net.Mail;
using System.Text;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Admin.Mentors;

internal static class MentorImportWorkbookParser
{
    internal const string EnterpriseSheetName = "DS Mentor_FA26";
    internal const string AcademicSheetName = "Mentor IT_FA26";
    private const int MaximumRowsPerSheet = 500;
    private const int MaximumHeaderSearchRows = 20;
    private const int MaximumColumns = 20;
    private static readonly IReadOnlyDictionary<string, string[]> EnterpriseColumns = new Dictionary<string, string[]>
    {
        ["stt"] = ["stt"],
        ["fullname"] = ["hovaten", "hoten"],
        ["dateofbirth"] = ["ngaythangnamsinh", "ngaysinh"],
        ["phone"] = ["sdt", "sodienthoai", "dienthoai"],
        ["contracttype"] = ["loaihd", "loaihopdong"],
        ["educationlevel"] = ["trinhdohocvan", "hocvan"],
        ["address"] = ["diachihiennay", "diachi"],
        ["email"] = ["email"],
        ["fptemail"] = ["fptemail", "emailfpt"],
        ["jobtitle"] = ["vitrichucdanh", "chucdanh", "vitri"],
        ["organization"] = ["congty", "tochuc", "donvi"]
    };
    private static readonly IReadOnlyDictionary<string, string[]> AcademicColumns = new Dictionary<string, string[]>
    {
        ["stt"] = ["stt"],
        ["email"] = ["emailcongviec", "email"],
        ["fullname"] = ["hoten", "hovaten"],
        ["department"] = ["phongbantructiep", "phongban", "bomon"],
        ["jobtitle"] = ["chucdanhvn", "chucdanh", "vitrichucdanh"]
    };

    static MentorImportWorkbookParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static Result<List<MentorImportCandidate>> Parse(IFormFile file)
    {
        try
        {
            using var source = file.OpenReadStream();
            using var content = new MemoryStream();
            source.CopyTo(content);
            content.Position = 0;
            using var reader = ExcelReaderFactory.CreateReader(content);

            var sheets = new Dictionary<string, IReadOnlyList<SpreadsheetRow>>(StringComparer.OrdinalIgnoreCase);
            do
            {
                var name = reader.Name?.Trim() ?? string.Empty;
                if (IsTargetSheet(name))
                {
                    var sheetRows = ReadSheet(reader, name);
                    if (sheetRows.IsFailure) return Result.Failure<List<MentorImportCandidate>>(sheetRows.Error);
                    sheets[name] = sheetRows.Value;
                }
                else
                {
                    while (reader.Read()) { }
                }
            } while (reader.NextResult());

            if (!TryGetSheet(sheets, EnterpriseSheetName, out var enterpriseRows) ||
                !TryGetSheet(sheets, AcademicSheetName, out var academicRows))
            {
                return Failure($"The workbook must contain both sheets '{EnterpriseSheetName}' and '{AcademicSheetName}'.");
            }

            var enterprise = ParseEnterprise(enterpriseRows!);
            if (enterprise.IsFailure) return Result.Failure<List<MentorImportCandidate>>(enterprise.Error);
            var academic = ParseAcademic(academicRows!);
            if (academic.IsFailure) return Result.Failure<List<MentorImportCandidate>>(academic.Error);

            var rows = enterprise.Value.Concat(academic.Value).ToList();
            var duplicateEmails = rows
                .Where(item => !string.IsNullOrWhiteSpace(item.Email))
                .GroupBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.Where(item => duplicateEmails.Contains(item.Email)))
                row.MarkInvalid($"Login email '{row.Email}' appears more than once in the workbook.");

            return Result.Success(rows);
        }
        catch
        {
            return Failure("The mentor workbook could not be read. Verify that it is a valid .xlsx file.");
        }
    }

    private static Result<List<SpreadsheetRow>> ReadSheet(IExcelDataReader reader, string sheetName)
    {
        var rows = new List<SpreadsheetRow>();
        var rowNumber = 0;
        while (reader.Read())
        {
            rowNumber++;
            if (reader.FieldCount > MaximumColumns)
                return RowFailure(sheetName, $"may contain at most {MaximumColumns} columns.");
            if (rowNumber > MaximumRowsPerSheet + MaximumHeaderSearchRows + 1)
                return RowFailure(sheetName, $"may contain at most {MaximumRowsPerSheet} data rows.");
            rows.Add(new SpreadsheetRow(rowNumber, Enumerable.Range(0, reader.FieldCount)
                .Select(index => CellText(reader.GetValue(index))).ToArray()));
        }
        return rows.Count == 0 ? RowFailure(sheetName, "contains no data.") : Result.Success(rows);
    }

    private static Result<List<MentorImportCandidate>> ParseEnterprise(IReadOnlyList<SpreadsheetRow> rows)
    {
        var header = FindHeader(rows, EnterpriseColumns, ["fullname"]);
        if (header is null) return Failure($"Sheet '{EnterpriseSheetName}' must contain a mentor name column such as 'Họ và tên'.");

        var dataRows = rows.Where(item => item.RowNumber > header.Value.Row.RowNumber && item.Cells.Any(value => !string.IsNullOrWhiteSpace(value))).ToArray();
        if (dataRows.Length > MaximumRowsPerSheet)
            return Failure($"Sheet '{EnterpriseSheetName}' may contain at most {MaximumRowsPerSheet} data rows.");
        var result = new List<MentorImportCandidate>();
        foreach (var row in dataRows)
        {
            var values = EnterpriseColumns.Keys.ToDictionary(key => key, key => GetOptionalCell(row.Cells, header.Value.Columns, key));
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            var rawEmail = values["email"];
            var email = NormalizeEmail(rawEmail);
            var candidate = new MentorImportCandidate
            {
                RowNumber = row.RowNumber,
                SheetName = EnterpriseSheetName,
                MentorType = MentorType.Enterprise,
                SourceOrdinal = EmptyToNull(values["stt"]),
                FullName = values["fullname"].Trim(),
                NormalizedFullName = NormalizeName(values["fullname"]),
                Email = email ?? rawEmail.Trim().ToLowerInvariant(),
                FptEmail = EmptyToNull(NormalizeEmail(values["fptemail"]) ?? values["fptemail"].Trim()),
                Phone = EmptyToNull(values["phone"]),
                DateOfBirth = ParseDate(values["dateofbirth"]),
                ContractType = EmptyToNull(values["contracttype"]),
                EducationLevel = EmptyToNull(values["educationlevel"]),
                CurrentAddress = EmptyToNull(values["address"]),
                JobTitle = EmptyToNull(values["jobtitle"]),
                Organization = EmptyToNull(values["organization"]),
                PresentColumns = header.Value.Columns.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            };
            ValidateCommon(candidate, email, rawEmail, values["dateofbirth"]);
            result.Add(candidate);
        }
        return result.Count == 0 ? Failure($"Sheet '{EnterpriseSheetName}' contains no mentor rows.") : Result.Success(result);
    }

    private static Result<List<MentorImportCandidate>> ParseAcademic(IReadOnlyList<SpreadsheetRow> rows)
    {
        var header = FindHeader(rows, AcademicColumns, ["fullname"]);
        if (header is null) return Failure($"Sheet '{AcademicSheetName}' must contain a mentor name column such as 'Họ tên'.");

        var dataRows = rows.Where(item => item.RowNumber > header.Value.Row.RowNumber && item.Cells.Any(value => !string.IsNullOrWhiteSpace(value))).ToArray();
        if (dataRows.Length > MaximumRowsPerSheet)
            return Failure($"Sheet '{AcademicSheetName}' may contain at most {MaximumRowsPerSheet} data rows.");
        var result = new List<MentorImportCandidate>();
        foreach (var row in dataRows)
        {
            var values = AcademicColumns.Keys.ToDictionary(key => key, key => GetOptionalCell(row.Cells, header.Value.Columns, key));
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            var rawEmail = values["email"];
            var email = NormalizeEmail(rawEmail);
            var candidate = new MentorImportCandidate
            {
                RowNumber = row.RowNumber,
                SheetName = AcademicSheetName,
                MentorType = MentorType.Academic,
                SourceOrdinal = EmptyToNull(values["stt"]),
                FullName = values["fullname"].Trim(),
                NormalizedFullName = NormalizeName(values["fullname"]),
                Email = email ?? rawEmail.Trim().ToLowerInvariant(),
                Department = EmptyToNull(values["department"]),
                JobTitle = EmptyToNull(values["jobtitle"]),
                PresentColumns = header.Value.Columns.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            };
            ValidateCommon(candidate, email, rawEmail, null);
            result.Add(candidate);
        }
        return result.Count == 0 ? Failure($"Sheet '{AcademicSheetName}' contains no mentor rows.") : Result.Success(result);
    }

    private static void ValidateCommon(MentorImportCandidate row, string? validEmail, string? rawEmail, string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(row.FullName)) row.MarkInvalid("Mentor name is required.");
        else if (row.FullName.Length > 100) row.MarkInvalid("Mentor name may contain at most 100 characters.");
        else if (!string.IsNullOrWhiteSpace(rawEmail) && validEmail is null) row.MarkInvalid("Login email must be a valid email address when provided.");
        else if (!string.IsNullOrWhiteSpace(rawDate) && row.DateOfBirth is null) row.MarkInvalid("Date of birth must be a valid date.");
        else if (row.DateOfBirth is { } date && (date > DateOnly.FromDateTime(DateTime.UtcNow) || date.Year < 1900)) row.MarkInvalid("Date of birth is outside the allowed range.");
        else if (row.FptEmail is not null && NormalizeEmail(row.FptEmail) is null) row.MarkInvalid("Fpt Email must be a valid email address when provided.");
        else if (row.Phone?.Length > 30) row.MarkInvalid("Phone number may contain at most 30 characters.");
        else if (row.ContractType?.Length > 100) row.MarkInvalid("Contract type may contain at most 100 characters.");
        else if (row.EducationLevel?.Length > 200) row.MarkInvalid("Education level may contain at most 200 characters.");
        else if (row.CurrentAddress?.Length > 500) row.MarkInvalid("Current address may contain at most 500 characters.");
        else if (row.Organization?.Length > 200) row.MarkInvalid("Organization may contain at most 200 characters.");
        else if (row.Department?.Length > 200) row.MarkInvalid("Department may contain at most 200 characters.");
        else if (row.JobTitle?.Length > 200) row.MarkInvalid("Job title may contain at most 200 characters.");
    }

    private static (SpreadsheetRow Row, Dictionary<string, int> Columns)? FindHeader(
        IReadOnlyList<SpreadsheetRow> rows,
        IReadOnlyDictionary<string, string[]> recognized,
        IReadOnlyCollection<string> required)
    {
        foreach (var row in rows.Take(MaximumHeaderSearchRows))
        {
            var sourceColumns = row.Cells.Select((value, index) => (Key: NormalizeHeader(value), Index: index))
                .Where(item => !string.IsNullOrEmpty(item.Key))
                .GroupBy(item => item.Key)
                .ToDictionary(group => group.Key, group => group.First().Index);
            var columns = recognized
                .Select(item => (item.Key, Match: item.Value.FirstOrDefault(sourceColumns.ContainsKey)))
                .Where(item => item.Match is not null)
                .ToDictionary(item => item.Key, item => sourceColumns[item.Match!], StringComparer.OrdinalIgnoreCase);
            if (required.All(columns.ContainsKey)) return (row, columns);
        }
        return null;
    }

    private static bool IsTargetSheet(string name) =>
        name.Equals(EnterpriseSheetName, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(AcademicSheetName, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetSheet(IReadOnlyDictionary<string, IReadOnlyList<SpreadsheetRow>> sheets, string name, out IReadOnlyList<SpreadsheetRow>? rows)
    {
        var match = sheets.FirstOrDefault(item => item.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
        rows = match.Value;
        return rows is not null;
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                builder.Append(character is 'Đ' or 'đ' ? 'd' : char.ToLowerInvariant(character));
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? NormalizeEmail(string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 || !MailAddress.TryCreate(email, out var parsed) ||
            !parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase)) return null;
        return parsed.Address.ToLowerInvariant();
    }

    private static DateOnly? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy"];
        return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)
            ? DateOnly.FromDateTime(exact)
            : DateTime.TryParse(value.Trim(), CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out var parsed)
                ? DateOnly.FromDateTime(parsed)
                : null;
    }

    private static string CellText(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };
    private static string GetCell(IReadOnlyList<string> cells, int index) => index < cells.Count ? cells[index].Trim() : string.Empty;
    private static string GetOptionalCell(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, string key) =>
        columns.TryGetValue(key, out var index) ? GetCell(cells, index) : string.Empty;
    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Normalize(NormalizationForm.FormKC)
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Result<List<MentorImportCandidate>> Failure(string message) => Result.Failure<List<MentorImportCandidate>>(new Error(ErrorCodes.MentorImportFileInvalid, message));
    private static Result<List<SpreadsheetRow>> RowFailure(string sheet, string message) => Result.Failure<List<SpreadsheetRow>>(new Error(ErrorCodes.MentorImportFileInvalid, $"Sheet '{sheet}' {message}"));

    private sealed record SpreadsheetRow(int RowNumber, IReadOnlyList<string> Cells);
}

internal sealed class MentorImportCandidate
{
    public int RowNumber { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public MentorType MentorType { get; set; }
    public string? SourceOrdinal { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string NormalizedFullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FptEmail { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? ContractType { get; set; }
    public string? EducationLevel { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Organization { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public HashSet<string> PresentColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Guid? DraftId { get; set; }
    public bool WillCreateAccount { get; set; }
    public bool WillUpdateAccount { get; set; }
    public bool WillSaveDraft { get; set; }
    public bool WillCompleteDraft { get; set; }
    public string Status { get; set; } = "Create";
    public bool IsValid { get; set; } = true;
    public string? Message { get; set; }

    public void MarkInvalid(string message)
    {
        IsValid = false;
        Status = "Conflict";
        Message = message;
    }

    public void ResetPlannedAction()
    {
        DraftId = null;
        WillCreateAccount = false;
        WillUpdateAccount = false;
        WillSaveDraft = false;
        WillCompleteDraft = false;
        Status = "Create";
        Message = null;
    }
}
