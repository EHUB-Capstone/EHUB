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

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = "Student_Import_Template.xlsx";
        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return Task.FromResult(Result.Success((bytes, contentType, fileName)));
    }
}
