using ClosedXML.Excel;
using EHub.Application.Features.Classes.ExportClassRoster;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors.ExportAssignments;

internal sealed record ExportMentor(Guid Id, string Name, MentorType Type, string? ContractType);

/// <summary>One team slot that has a mentor in effect, tagged with the subject of the team's class.</summary>
internal sealed record ExportTeamMentor(Guid MentorId, string SubjectCode);

/// <summary>
/// Builds the semester mentor assignment workbook: one roster sheet per subject (same layout as the class roster
/// export) and a summary sheet that counts the teams of every mentor per subject.
/// </summary>
internal static class MentorAssignmentExportWorkbookBuilder
{
    internal const string FirstSubjectCode = "EXE101";
    internal const string SecondSubjectCode = "EXE201";
    internal const string SummarySheetName = "Tổng hợp";
    internal const string UnassignedLabel = "Chưa phân công";
    internal const string AcademicTitle = "GIẢNG VIÊN IT";
    internal const string TotalLabel = "TỔNG";

    internal static byte[] Build(
        string? semesterCode,
        IReadOnlyCollection<ClassRosterExportSection> firstSubjectSections,
        IReadOnlyCollection<ClassRosterExportSection> secondSubjectSections,
        IReadOnlyDictionary<string, string>? registeredMajorByEmail,
        IReadOnlyCollection<ExportMentor> mentors,
        IReadOnlyCollection<ExportTeamMentor> teamMentors)
    {
        using var workbook = new XLWorkbook();

        ClassRosterExportWorkbookBuilder.WriteSheet(
            workbook.Worksheets.Add(FirstSubjectCode), firstSubjectSections, registeredMajorByEmail, semesterCode, UnassignedLabel);
        ClassRosterExportWorkbookBuilder.WriteSheet(
            workbook.Worksheets.Add(SecondSubjectCode), secondSubjectSections, registeredMajorByEmail, semesterCode, UnassignedLabel);
        WriteSummary(workbook.Worksheets.Add(SummarySheetName), mentors, teamMentors);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummary(
        IXLWorksheet worksheet,
        IReadOnlyCollection<ExportMentor> mentors,
        IReadOnlyCollection<ExportTeamMentor> teamMentors)
    {
        var countsByMentor = teamMentors
            .GroupBy(item => item.MentorId)
            .ToDictionary(
                group => group.Key,
                group => (
                    First: group.Count(item => IsSubject(item.SubjectCode, FirstSubjectCode)),
                    Second: group.Count(item => IsSubject(item.SubjectCode, SecondSubjectCode))));

        // Enterprise mentors: header row, one row per mentor, total row.
        string[] headers = ["STT", "Họ và tên", $"Nhóm {FirstSubjectCode}", $"Nhóm {SecondSubjectCode}", "Tổng", "Loại HĐ"];
        for (var column = 0; column < headers.Length; column++) worksheet.Cell(1, column + 1).Value = headers[column];
        StyleHeader(worksheet.Range(1, 1, 1, headers.Length));
        var row = WriteMentorRows(worksheet, 2, mentors.Where(item => item.Type == MentorType.Enterprise), countsByMentor, includeContract: true);
        var enterpriseLastRow = row - 1;
        ApplyBorders(worksheet.Range(1, 1, enterpriseLastRow, headers.Length));

        // Academic mentors ("Giảng viên IT"): yellow title row, then the same columns without the contract type.
        row++;
        var titleRow = row;
        worksheet.Cell(titleRow, 1).Value = AcademicTitle;
        var title = worksheet.Range(titleRow, 1, titleRow, 5);
        title.Style.Font.Bold = true;
        title.Style.Fill.BackgroundColor = XLColor.Yellow;
        row = WriteMentorRows(worksheet, titleRow + 1, mentors.Where(item => item.Type == MentorType.Academic), countsByMentor, includeContract: false);
        ApplyBorders(worksheet.Range(titleRow, 1, row - 1, 5));

        worksheet.Columns().AdjustToContents();
    }

    private static int WriteMentorRows(
        IXLWorksheet worksheet,
        int startRow,
        IEnumerable<ExportMentor> mentors,
        IReadOnlyDictionary<Guid, (int First, int Second)> countsByMentor,
        bool includeContract)
    {
        var row = startRow;
        var index = 1;
        var totalFirst = 0;
        var totalSecond = 0;

        foreach (var mentor in mentors.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var counts = countsByMentor.GetValueOrDefault(mentor.Id);
            worksheet.Cell(row, 1).Value = index++;
            worksheet.Cell(row, 2).Value = mentor.Name;
            worksheet.Cell(row, 3).Value = counts.First;
            worksheet.Cell(row, 4).Value = counts.Second;
            worksheet.Cell(row, 5).Value = counts.First + counts.Second;
            if (includeContract) worksheet.Cell(row, 6).Value = mentor.ContractType ?? string.Empty;
            totalFirst += counts.First;
            totalSecond += counts.Second;
            row++;
        }

        worksheet.Cell(row, 2).Value = TotalLabel;
        worksheet.Cell(row, 3).Value = totalFirst;
        worksheet.Cell(row, 4).Value = totalSecond;
        worksheet.Cell(row, 5).Value = totalFirst + totalSecond;
        worksheet.Range(row, 1, row, includeContract ? 6 : 5).Style.Font.Bold = true;
        return row + 1;
    }

    private static bool IsSubject(string code, string expected) =>
        string.Equals(code, expected, StringComparison.OrdinalIgnoreCase);

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
    }

    private static void ApplyBorders(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }
}
