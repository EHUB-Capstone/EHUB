using System.Globalization;
using ExcelDataReader;
using EHub.Application.Features.Classes.Common;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Classes.ImportSemesterGroups;

internal static class SemesterGroupImportWorkbookParser
{
    internal const int MaximumRows = 5_000;
    internal const int MaximumGroupNameLength = 100;

    static SemesterGroupImportWorkbookParser()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    internal static Result<IReadOnlyCollection<SemesterGroupSourceRow>> Parse(
        IFormFile file,
        string expectedColumnName)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);
            if (!reader.Read()) return Failure("The Excel worksheet contains no data.");

            var rollNumberColumn = -1;
            var groupColumn = -1;
            var expectedHeader = SemesterGroupColumn.NormalizeHeader(expectedColumnName);
            for (var column = 0; column < reader.FieldCount; column++)
            {
                var header = SemesterGroupColumn.NormalizeHeader(GetText(reader.GetValue(column)));
                if (header is "ROLLNUMBER" or "STUDENTCODE" or "MSSV") rollNumberColumn = column;
                if (header == expectedHeader) groupColumn = column;
            }

            if (rollNumberColumn < 0 || groupColumn < 0)
            {
                return Failure($"Excel header must contain RollNumber and {expectedColumnName} columns.");
            }

            var rows = new List<SemesterGroupSourceRow>();
            var rowNumber = 1;
            while (reader.Read())
            {
                rowNumber++;
                if (rows.Count >= MaximumRows)
                {
                    return Failure($"A semester group file can contain at most {MaximumRows} data rows.");
                }

                var rollNumber = GetText(reader.GetValue(rollNumberColumn)).ToUpperInvariant();
                var groupName = GetText(reader.GetValue(groupColumn));
                if (string.IsNullOrWhiteSpace(rollNumber) && string.IsNullOrWhiteSpace(groupName)) continue;

                rows.Add(new SemesterGroupSourceRow(rowNumber, rollNumber, groupName));
            }

            if (rows.Count == 0) return Failure("The Excel worksheet contains no semester group data rows.");
            return Result.Success<IReadOnlyCollection<SemesterGroupSourceRow>>(rows);
        }
        catch
        {
            return Failure("Failed to parse the Excel semester group file.");
        }
    }

    private static string GetText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static Result<IReadOnlyCollection<SemesterGroupSourceRow>> Failure(string message) =>
        Result.Failure<IReadOnlyCollection<SemesterGroupSourceRow>>(
            new Error("Classes.InvalidExcelFormat", message));
}

internal sealed record SemesterGroupSourceRow(int RowNumber, string RollNumber, string GroupName);
