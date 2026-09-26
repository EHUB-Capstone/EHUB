using ClosedXML.Excel;

namespace EHub.Application.Features.Admin.Mentors;

internal static class MentorImportTemplateBuilder
{
    public static byte[] Build()
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.Worksheets.Add(MentorImportWorkbookParser.EnterpriseSheetName);
        string[] enterpriseHeaders = ["STT", "Họ và tên", "Ngày tháng năm sinh", "SDT", "Loại HĐ", "Trình độ học vấn", "Địa chỉ hiện nay", "Email", "Fpt Email", "Vị trí, Chức danh", "Công ty"];
        string[] enterpriseExample = ["1", "Nguyễn Văn Minh", "15/05/1985", "0900000001", "Thỉnh giảng", "Thạc sĩ", "Đà Nẵng", "minh.nguyen@example.com", "", "Giám đốc sản phẩm", "Công ty Công nghệ Mẫu"];
        Populate(enterprise, enterpriseHeaders, enterpriseExample);

        var academic = workbook.Worksheets.Add(MentorImportWorkbookParser.AcademicSheetName);
        string[] academicHeaders = ["STT", "Email công việc", "Họ tên", "Phòng ban trực tiếp", "Chức danh (VN)"];
        string[] academicExample = ["1", "lan.tran@example.edu.vn", "Trần Thị Lan", "Bộ môn Công nghệ thông tin", "Giảng viên"];
        Populate(academic, academicHeaders, academicExample);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Populate(IXLWorksheet worksheet, IReadOnlyList<string> headers, IReadOnlyList<string> example)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
            worksheet.Cell(2, index + 1).Value = example[index];
        }
        var header = worksheet.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();
        worksheet.RangeUsed()?.SetAutoFilter();
    }
}
