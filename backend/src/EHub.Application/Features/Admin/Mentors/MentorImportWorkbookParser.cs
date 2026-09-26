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
        var required = new[] { "stt", "hovaten", "ngaythangnamsinh", "sdt", "loaihd", "trinhdohocvan", "diachihiennay", "email", "fptemail", "vitrichucdanh", "congty" };
        var header = FindHeader(rows, required);
        if (header is null) return Failure($"Sheet '{EnterpriseSheetName}' does not contain the required enterprise mentor headers.");

        var dataRows = rows.Where(item => item.RowNumber > header.Value.Row.RowNumber && item.Cells.Any(value => !string.IsNullOrWhiteSpace(value))).ToArray();
        if (dataRows.Length > MaximumRowsPerSheet)
            return Failure($"Sheet '{EnterpriseSheetName}' may contain at most {MaximumRowsPerSheet} data rows.");
        var result = new List<MentorImportCandidate>();
        foreach (var row in dataRows)
        {
            var values = required.ToDictionary(key => key, key => GetCell(row.Cells, header.Value.Columns[key]));
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            var email = NormalizeEmail(values["email"]);
            var candidate = new MentorImportCandidate
            {
                RowNumber = row.RowNumber,
                SheetName = EnterpriseSheetName,
                MentorType = MentorType.Enterprise,
                FullName = values["hovaten"].Trim(),
                Email = email ?? values["email"].Trim().ToLowerInvariant(),
                FptEmail = EmptyToNull(NormalizeEmail(values["fptemail"]) ?? values["fptemail"].Trim()),
                Phone = EmptyToNull(values["sdt"]),
                DateOfBirth = ParseDate(values["ngaythangnamsinh"]),
                ContractType = EmptyToNull(values["loaihd"]),
                EducationLevel = EmptyToNull(values["trinhdohocvan"]),
                CurrentAddress = EmptyToNull(values["diachihiennay"]),
                JobTitle = EmptyToNull(values["vitrichucdanh"]),
                Organization = EmptyToNull(values["congty"])
            };
            ValidateCommon(candidate, email, values["ngaythangnamsinh"]);
            result.Add(candidate);
        }
        return result.Count == 0 ? Failure($"Sheet '{EnterpriseSheetName}' contains no mentor rows.") : Result.Success(result);
    }

    private static Result<List<MentorImportCandidate>> ParseAcademic(IReadOnlyList<SpreadsheetRow> rows)
    {
        var required = new[] { "stt", "emailcongviec", "hoten", "phongbantructiep", "chucdanhvn" };
        var header = FindHeader(rows, required);
        if (header is null) return Failure($"Sheet '{AcademicSheetName}' does not contain the required academic mentor headers.");

        var dataRows = rows.Where(item => item.RowNumber > header.Value.Row.RowNumber && item.Cells.Any(value => !string.IsNullOrWhiteSpace(value))).ToArray();
        if (dataRows.Length > MaximumRowsPerSheet)
            return Failure($"Sheet '{AcademicSheetName}' may contain at most {MaximumRowsPerSheet} data rows.");
        var result = new List<MentorImportCandidate>();
        foreach (var row in dataRows)
        {
            var values = required.ToDictionary(key => key, key => GetCell(row.Cells, header.Value.Columns[key]));
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            var email = NormalizeEmail(values["emailcongviec"]);
            var candidate = new MentorImportCandidate
            {
                RowNumber = row.RowNumber,
                SheetName = AcademicSheetName,
                MentorType = MentorType.Academic,
                FullName = values["hoten"].Trim(),
                Email = email ?? values["emailcongviec"].Trim().ToLowerInvariant(),
                Department = EmptyToNull(values["phongbantructiep"]),
                JobTitle = EmptyToNull(values["chucdanhvn"])
            };
            ValidateCommon(candidate, email, null);
            result.Add(candidate);
        }
        return result.Count == 0 ? Failure($"Sheet '{AcademicSheetName}' contains no mentor rows.") : Result.Success(result);
    }

    private static void ValidateCommon(MentorImportCandidate row, string? validEmail, string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(row.FullName)) row.MarkInvalid("Mentor name is required.");
        else if (row.FullName.Length > 100) row.MarkInvalid("Mentor name may contain at most 100 characters.");
        else if (validEmail is null) row.MarkInvalid("A valid login email is required.");
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

    private static (SpreadsheetRow Row, Dictionary<string, int> Columns)? FindHeader(IReadOnlyList<SpreadsheetRow> rows, IReadOnlyCollection<string> required)
    {
        foreach (var row in rows.Take(MaximumHeaderSearchRows))
        {
            var columns = row.Cells.Select((value, index) => (Key: NormalizeHeader(value), Index: index))
                .Where(item => !string.IsNullOrEmpty(item.Key))
                .GroupBy(item => item.Key)
                .ToDictionary(group => group.Key, group => group.First().Index);
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
    public string FullName { get; set; } = string.Empty;
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
    public string Status { get; set; } = "Create";
    public bool IsValid { get; set; } = true;
    public string? Message { get; set; }

    public void MarkInvalid(string message)
    {
        IsValid = false;
        Status = "Conflict";
        Message = message;
    }
}
