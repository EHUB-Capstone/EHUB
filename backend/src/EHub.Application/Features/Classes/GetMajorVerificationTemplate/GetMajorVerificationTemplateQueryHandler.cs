using ClosedXML.Excel;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.GetMajorVerificationTemplate;

public sealed class GetMajorVerificationTemplateQueryHandler : IGetMajorVerificationTemplateQueryHandler
{
    public Result<(byte[] FileBytes, string ContentType, string FileName)> Handle()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Major Verification");
        worksheet.Cell(1, 1).Value = "StudentCode";
        worksheet.Cell(1, 2).Value = "MajorCode";
        worksheet.Cell(2, 1).Value = "SE123456";
        worksheet.Cell(2, 2).Value = "BIT_SE";
        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Result.Success((
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Major_Verification_Template.xlsx"));
    }
}
