using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.GetImportTemplate;

public sealed class GetImportTemplateQueryHandler : IGetImportTemplateQueryHandler
{
    public Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Student Import Template");

        // Headers
        worksheet.Cell(1, 1).Value = "StudentCode";
        worksheet.Cell(1, 2).Value = "FullName";
        worksheet.Cell(1, 3).Value = "Email";
        worksheet.Cell(1, 4).Value = "MajorCode";
        worksheet.Cell(1, 5).Value = "Group";
        worksheet.Cell(1, 6).Value = "Project";
        worksheet.Cell(1, 7).Value = "Zalo";
        worksheet.Cell(1, 8).Value = "Description";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        // Sample Data Rows
        worksheet.Cell(2, 1).Value = "SE170001";
        worksheet.Cell(2, 2).Value = "Nguyen Van A";
        worksheet.Cell(2, 3).Value = "anv@fpt.edu.vn";
        worksheet.Cell(2, 4).Value = "BIT_SE";

        worksheet.Cell(3, 1).Value = "SE170002";
        worksheet.Cell(3, 2).Value = "Tran Thi B";
        worksheet.Cell(3, 3).Value = "btt@fpt.edu.vn";
        worksheet.Cell(3, 4).Value = "BBA_MKT";

        worksheet.Columns().AdjustToContents();
        worksheet.Column(8).Width = 48;

        var guide = workbook.Worksheets.Add("Import Guide");
        guide.Cell(1, 1).Value = "Mode";
        guide.Cell(1, 2).Value = "How to use the template";
        guide.Cell(2, 1).Value = "Student roster";
        guide.Cell(2, 2).Value = "Leave Group blank. Valid rows add or re-enroll students in the class.";
        guide.Cell(3, 1).Value = "Team assignment";
        guide.Cell(3, 2).Value = "Fill Group and Project for every student. EHUB adds or re-enrolls the students in the class, then creates each 4-6 member team. No major-composition rule is applied.";
        guide.Cell(4, 1).Value = "Project details";
        guide.Cell(4, 2).Value = "Zalo and Description are optional. Enter each value once per Group, or repeat the same value on multiple rows. Conflicting values in one Group are rejected.";
        guide.Row(1).Style.Font.Bold = true;
        guide.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        guide.Column(1).Width = 20;
        guide.Column(2).Width = 90;
        guide.Range(1, 1, 4, 2).Style.Alignment.WrapText = true;
        guide.Rows(2, 4).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = "Student_Import_Template.xlsx";
        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return Task.FromResult(Result.Success((bytes, contentType, fileName)));
    }
}
