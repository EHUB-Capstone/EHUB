using System.Globalization;
using System.Text;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.StartupIndustries.ImportStartupIndustries;

internal static class StartupIndustryImportWorkbookParser
{
    private const int MaximumDataRows = 500;
    private const int MaximumColumns = 10;
    private const int MaximumHeaderSearchRows = 10;

    private static readonly HashSet<string> IndustryNameHeaders =
        ["industryname", "industry"];

    private static readonly HashSet<string> DescriptionHeaders =
        ["description"];

    static StartupIndustryImportWorkbookParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Result<List<StartupIndustryImportCandidate>> Parse(IFormFile file)
    {
        try
        {
            using var source = file.OpenReadStream();
            using var content = new MemoryStream();
            source.CopyTo(content);
            content.Position = 0;

            using var reader = ExcelReaderFactory.CreateReader(content);
            var rows = ReadFirstWorksheet(reader);
            return rows.IsFailure
                ? Result.Failure<List<StartupIndustryImportCandidate>>(rows.Error)
                : ParseRows(rows.Value);
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
                    ErrorCodes.StartupIndustryImportFileInvalid,
                    $"The industry import may contain at most {MaximumColumns} columns."));
            }

            if (rowNumber > MaximumDataRows + MaximumHeaderSearchRows + 1)
            {
                return Result.Failure<List<SpreadsheetRow>>(new Error(
                    ErrorCodes.StartupIndustryImportFileInvalid,
                    $"The industry import may contain at most {MaximumDataRows} data rows."));
            }

            rows.Add(new SpreadsheetRow(
                rowNumber,
                Enumerable.Range(0, reader.FieldCount)
                    .Select(column => CellText(reader.GetValue(column)))
                    .ToArray()));
        }

        return rows.Count == 0
            ? Result.Failure<List<SpreadsheetRow>>(new Error(
                ErrorCodes.StartupIndustryImportFileInvalid,
                "The Excel worksheet contains no data."))
            : Result.Success(rows);
    }

    private static Result<List<StartupIndustryImportCandidate>> ParseRows(
        IReadOnlyList<SpreadsheetRow> sourceRows)
    {
        var header = sourceRows
            .Take(MaximumHeaderSearchRows)
            .Select(row => (Row: row, Columns: FindColumns(row.Cells)))
            .FirstOrDefault(item => item.Columns.Name >= 0 && item.Columns.Description >= 0);

        if (header.Row is null)
        {
            return Failure("The header must contain 'Industry name' (or 'Industry') and 'Description' columns.");
        }

        var dataRows = sourceRows.Where(row => row.RowNumber > header.Row.RowNumber).ToArray();
        if (dataRows.Length > MaximumDataRows)
        {
            return Failure($"The industry import may contain at most {MaximumDataRows} data rows.");
        }

        var candidates = new List<StartupIndustryImportCandidate>();
        foreach (var row in dataRows)
        {
            var name = GetCell(row.Cells, header.Columns.Name);
            var description = GetCell(row.Cells, header.Columns.Description);

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            name = name.Trim();
            description = description.Trim();
            var error = string.IsNullOrWhiteSpace(name)
                ? "Industry name is required."
                : name.Length > 100
                    ? "Industry name may contain at most 100 characters."
                    : description.Length > 240
                        ? "Description may contain at most 240 characters."
                        : null;

            candidates.Add(new StartupIndustryImportCandidate(
                row.RowNumber,
                name,
                string.IsNullOrWhiteSpace(description) ? null : description,
                NormalizeName(name),
                error is null,
                error is null ? "Ready" : "Error",
                error,
                error is null ? null : ErrorCodes.StartupIndustryImportFileInvalid));
        }

        if (candidates.Count == 0)
        {
            return Failure("The Excel worksheet contains no industry rows.");
        }

        var duplicateNames = candidates
            .Where(candidate => candidate.IsValid)
            .GroupBy(candidate => candidate.NormalizedName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!candidate.IsValid || !duplicateNames.Contains(candidate.NormalizedName)) continue;

            candidates[index] = candidate with
            {
                IsValid = false,
                Status = "Error",
                ErrorMessage = $"Industry '{candidate.Name}' appears more than once in the file.",
                ErrorCode = ErrorCodes.StartupIndustryImportFileInvalid
            };
        }

        return Result.Success(candidates);
    }

    private static IndustryColumns FindColumns(IReadOnlyList<string> cells)
    {
        var name = -1;
        var description = -1;

        for (var index = 0; index < cells.Count; index++)
        {
            var header = NormalizeHeader(cells[index]);
            if (name < 0 && IndustryNameHeaders.Contains(header)) name = index;
            else if (description < 0 && DescriptionHeaders.Contains(header)) description = index;
        }

        return new IndustryColumns(name, description);
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

    private static string NormalizeName(string value) => value.Trim().ToUpperInvariant();

    private static Result<List<StartupIndustryImportCandidate>> Failure(string message) =>
        Result.Failure<List<StartupIndustryImportCandidate>>(new Error(
            ErrorCodes.StartupIndustryImportFileInvalid,
            message));

    private sealed record SpreadsheetRow(int RowNumber, IReadOnlyList<string> Cells);
    private sealed record IndustryColumns(int Name, int Description);
}

internal sealed record StartupIndustryImportCandidate(
    int RowNumber,
    string Name,
    string? Description,
    string NormalizedName,
    bool IsValid,
    string Status,
    string? ErrorMessage,
    string? ErrorCode);
