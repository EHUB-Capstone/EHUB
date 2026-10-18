using ClosedXML.Excel;

namespace EHub.Application.Features.Workspaces.EvaluationReportExport;

internal sealed record EvaluationReportColumn(Guid CourseId, Guid RubricId, string Name, int SortOrder);

internal sealed record EvaluationReportStudentRow(
    string RollNumber,
    string FullName,
    Guid CourseId,
    Guid? ProjectId,
    Guid StudentId,
    IReadOnlySet<Guid> IncludedRubricIds);

internal static class EvaluationReportWorkbookBuilder
{
    internal const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    internal const string WorksheetName = "Evaluation Report";

    // MSSV and Họ Tên come first, so the score columns start at the third column.
    private const int FirstScoreColumn = 3;

    internal static byte[] Build(
        IReadOnlyCollection<EvaluationReportColumn> columns,
        IReadOnlyCollection<EvaluationReportStudentRow> students,
        IReadOnlyDictionary<(Guid ProjectId, Guid RubricId, Guid StudentId), decimal> scores)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(WorksheetName);
        var orderedColumns = columns
            .OrderBy(column => column.SortOrder)
            .ThenBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(column => column.RubricId)
            .ToArray();

        worksheet.Cell(1, 1).Value = "MSSV";
        worksheet.Cell(1, 2).Value = "Họ Tên";
        for (var index = 0; index < orderedColumns.Length; index++)
        {
            worksheet.Cell(1, index + FirstScoreColumn).Value = orderedColumns[index].Name;
        }

        var rowNumber = 2;
        foreach (var student in students)
        {
            worksheet.Cell(rowNumber, 1).Value = student.RollNumber;
            worksheet.Cell(rowNumber, 2).Value = student.FullName;
            for (var index = 0; index < orderedColumns.Length; index++)
            {
                var column = orderedColumns[index];
                if (student.CourseId != column.CourseId || student.ProjectId is null ||
                    !student.IncludedRubricIds.Contains(column.RubricId))
                {
                    continue;
                }

                if (scores.TryGetValue((student.ProjectId.Value, column.RubricId, student.StudentId), out var score))
                {
                    var cell = worksheet.Cell(rowNumber, index + FirstScoreColumn);
                    cell.Value = score;
                    cell.Style.NumberFormat.Format = "0.00";
                }
            }
            rowNumber++;
        }

        var lastColumn = orderedColumns.Length + FirstScoreColumn - 1;
        var lastRow = Math.Max(1, rowNumber - 1);
        var usedRange = worksheet.Range(1, 1, lastRow, lastColumn);
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(1, 1, 1, lastColumn).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, lastRow, lastColumn).SetAutoFilter();
        worksheet.Columns(1, lastColumn).AdjustToContents();
        for (var columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
        {
            if (worksheet.Column(columnNumber).Width > 35)
            {
                worksheet.Column(columnNumber).Width = 35;
                worksheet.Column(columnNumber).Style.Alignment.WrapText = true;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
