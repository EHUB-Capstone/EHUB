using System.Globalization;
using System.Net.Mail;
using System.Text;
using ExcelDataReader;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Admin.Users.ImportLecturers;

internal static class LecturerImportWorkbookParser
{
    private const int MaximumDataRows = 500;
    private const int MaximumColumns = 16;
    private const int MaximumHeaderSearchRows = 20;

    private static readonly HashSet<string> FullNameHeaders =
        ["tengiangvien", "hovaten", "fullname", "name"];

    private static readonly HashSet<string> PositionHeaders =
        ["vitri", "position", "title"];

    private static readonly HashSet<string> RoleHeaders =
        ["role", "roles", "vaitro"];

    private static readonly HashSet<string> LoginEmailHeaders =
        ["googleemail", "loginemail", "emaildangnhap", "emailgoogle"];

    private static readonly HashSet<string> EmailHeaders =
        ["email", "emailaddress", "mail"];

    static LecturerImportWorkbookParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Result<List<LecturerImportCandidate>> Parse(IFormFile file)
    {
        try
        {
            using var source = file.OpenReadStream();
            using var content = new MemoryStream();
            source.CopyTo(content);
            content.Position = 0;

            using var reader = ExcelReaderFactory.CreateReader(content);
            var rows = ReadFirstWorksheet(reader);
            if (rows.IsFailure)
            {
                return Result.Failure<List<LecturerImportCandidate>>(rows.Error);
            }

            return ParseRows(rows.Value);
        }
        catch
        {
            return Failure("The Excel file could not be read. Verify that it is a valid .xlsx or .xls workbook.");
        }
    }

    private static Result<List<SpreadsheetRow>> ReadFirstWorksheet(IExcelDataReader reader)
    {
        var rows = new List<SpreadsheetRow>();
        var rowNumber = 0;

        while (reader.Read())
        {
            rowNumber++;
            if (reader.FieldCount > MaximumColumns)
            {
                return Result.Failure<List<SpreadsheetRow>>(new Error(
                    ErrorCodes.LecturerImportFileInvalid,
                    $"The lecturer import may contain at most {MaximumColumns} columns."));
            }

            if (rowNumber > MaximumDataRows + MaximumHeaderSearchRows + 1)
            {
                return Result.Failure<List<SpreadsheetRow>>(new Error(
                    ErrorCodes.LecturerImportFileInvalid,
                    $"The lecturer import may contain at most {MaximumDataRows} data rows."));
            }

            rows.Add(new SpreadsheetRow(
                rowNumber,
                Enumerable.Range(0, reader.FieldCount)
                    .Select(column => CellText(reader.GetValue(column)))
                    .ToArray()));
        }

        return rows.Count == 0
            ? Result.Failure<List<SpreadsheetRow>>(new Error(
                ErrorCodes.LecturerImportFileInvalid,
                "The Excel worksheet contains no data."))
            : Result.Success(rows);
    }

    private static Result<List<LecturerImportCandidate>> ParseRows(IReadOnlyList<SpreadsheetRow> sourceRows)
    {
        var header = sourceRows
            .Take(MaximumHeaderSearchRows)
            .Select(row => (Row: row, Columns: FindColumns(row.Cells)))
            .FirstOrDefault(item => item.Columns.FullName >= 0 && item.Columns.Role >= 0 &&
                                    (item.Columns.EmailColumns.Count > 0 || item.Columns.LegacyLoginEmail >= 0));

        if (header.Row is null)
        {
            return Failure("The header must contain lecturer name, email, and role columns.");
        }

        var dataRows = sourceRows.Where(row => row.RowNumber > header.Row.RowNumber).ToArray();
        if (dataRows.Length > MaximumDataRows)
        {
            return Failure($"The lecturer import may contain at most {MaximumDataRows} data rows.");
        }

        var candidates = new List<LecturerImportCandidate>();
        foreach (var row in dataRows)
        {
            var rawName = GetCell(row.Cells, header.Columns.FullName);
            var rawPosition = GetCell(row.Cells, header.Columns.Position);
            var rawRole = GetCell(row.Cells, header.Columns.Role);
            var preferredEmail = GetCell(row.Cells, header.Columns.PreferredLoginEmail);
            var allEmailValues = header.Columns.EmailColumns
                .Select(index => GetCell(row.Cells, index))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (string.IsNullOrWhiteSpace(rawName) &&
                string.IsNullOrWhiteSpace(rawPosition) &&
                string.IsNullOrWhiteSpace(rawRole) &&
                string.IsNullOrWhiteSpace(preferredEmail) &&
                allEmailValues.Length == 0)
            {
                continue;
            }

            var loginEmail = NormalizeValidEmail(preferredEmail) ??
                             allEmailValues.Select(NormalizeValidEmail).FirstOrDefault(email => email is not null) ??
                             preferredEmail.Trim().ToLowerInvariant();

            var contactEmail = allEmailValues
                .Select(NormalizeValidEmail)
                .FirstOrDefault(email => email is not null &&
                                         !string.Equals(email, loginEmail, StringComparison.OrdinalIgnoreCase));

            var error = ValidateRow(rawName, rawRole, loginEmail);
            candidates.Add(new LecturerImportCandidate
            {
                RowNumber = row.RowNumber,
                FullName = rawName.Trim(),
                Position = EmptyToNull(rawPosition),
                ContactEmail = contactEmail,
                GoogleEmail = loginEmail,
                IsValid = error is null,
                Status = error is null ? "Ready" : "Invalid",
                Message = error
            });
        }

        if (candidates.Count == 0)
        {
            return Failure("The Excel worksheet contains no lecturer rows.");
        }

        var duplicateEmails = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.GoogleEmail))
            .GroupBy(candidate => candidate.GoogleEmail, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates.Where(candidate => duplicateEmails.Contains(candidate.GoogleEmail)))
        {
            candidate.IsValid = false;
            candidate.Status = "Invalid";
            candidate.Message = $"Login email '{candidate.GoogleEmail}' appears more than once in the file.";
        }

        return Result.Success(candidates);
    }

    private static LecturerColumns FindColumns(IReadOnlyList<string> cells)
    {
        var fullName = -1;
        var position = -1;
        var role = -1;
        var explicitLoginEmail = -1;
        var emailColumns = new List<int>();

        for (var index = 0; index < cells.Count; index++)
        {
            var header = NormalizeHeader(cells[index]);
            if (FullNameHeaders.Contains(header)) fullName = index;
            else if (PositionHeaders.Contains(header)) position = index;
            else if (RoleHeaders.Contains(header)) role = index;
            else if (LoginEmailHeaders.Contains(header)) explicitLoginEmail = index;
            else if (EmailHeaders.Contains(header)) emailColumns.Add(index);
        }

        var legacyLoginEmail = role > 0 && string.IsNullOrWhiteSpace(GetCell(cells, role - 1))
            ? role - 1
            : -1;

        var preferredLoginEmail = explicitLoginEmail >= 0
            ? explicitLoginEmail
            : legacyLoginEmail >= 0
                ? legacyLoginEmail
                : emailColumns.LastOrDefault(-1);

        var readableEmailColumns = emailColumns
            .Append(preferredLoginEmail)
            .Where(index => index >= 0)
            .Distinct()
            .ToArray();

        return new LecturerColumns(
            fullName,
            position,
            role,
            legacyLoginEmail,
            preferredLoginEmail,
            readableEmailColumns);
    }

    private static string? ValidateRow(string name, string role, string email)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Lecturer name is required.";
        if (name.Trim().Length > 100) return "Lecturer name may contain at most 100 characters.";
        if (string.IsNullOrWhiteSpace(email) || NormalizeValidEmail(email) is null) return "A valid login email is required.";
        if (email.Length > 320) return "Login email may contain at most 320 characters.";
        if (!string.IsNullOrWhiteSpace(role) && !role.Trim().Equals("Lecturer", StringComparison.OrdinalIgnoreCase))
        {
            return "Role must be Lecturer when the role column is provided.";
        }

        return null;
    }

    private static string? NormalizeValidEmail(string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 ||
            !MailAddress.TryCreate(email, out var parsed) ||
            !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return parsed.Address.ToLowerInvariant();
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CellText(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };

    private static string GetCell(IReadOnlyList<string> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index].Trim() : string.Empty;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<List<LecturerImportCandidate>> Failure(string message) =>
        Result.Failure<List<LecturerImportCandidate>>(new Error(ErrorCodes.LecturerImportFileInvalid, message));

    private sealed record SpreadsheetRow(int RowNumber, IReadOnlyList<string> Cells);

    private sealed record LecturerColumns(
        int FullName,
        int Position,
        int Role,
        int LegacyLoginEmail,
        int PreferredLoginEmail,
        IReadOnlyList<int> EmailColumns);
}

internal sealed class LecturerImportCandidate
{
    public int RowNumber { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Position { get; init; }
    public string? ContactEmail { get; init; }
    public string GoogleEmail { get; init; } = string.Empty;
    public bool IsValid { get; set; }
    public string Status { get; set; } = "Ready";
    public string? Message { get; set; }
}
