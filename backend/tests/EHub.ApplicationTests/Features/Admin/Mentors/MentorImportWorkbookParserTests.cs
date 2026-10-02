using ClosedXML.Excel;
using EHub.Application.Features.Admin.Mentors;
using EHub.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorImportWorkbookParserTests
{
    [Fact]
    public void Parse_ValidTwoSheetWorkbook_ReadsBothMentorTypesAndOptionalFptEmail()
    {
        var file = CreateWorkbook((enterprise, academic) =>
        {
            WriteEnterpriseHeader(enterprise);
            WriteEnterpriseRow(enterprise, 2, "Mentor Doanh nghiệp", "21/01/1993", "mentor@example.com", string.Empty);
            WriteAcademicHeader(academic);
            WriteAcademicRow(academic, 2, "academic@example.edu.vn", "Mentor Giảng viên");
        });

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Should().HaveCount(2);
        result.Value.Should().ContainSingle(item => item.MentorType == MentorType.Enterprise && item.FptEmail == null && item.DateOfBirth == new DateOnly(1993, 1, 21));
        result.Value.Should().ContainSingle(item => item.MentorType == MentorType.Academic && item.Email == "academic@example.edu.vn");
    }

    [Fact]
    public void Parse_SheetsInReverseOrder_StillUsesNames()
    {
        var file = CreateWorkbook((enterprise, academic) =>
        {
            WriteEnterpriseHeader(enterprise);
            WriteEnterpriseRow(enterprise, 2, "Enterprise", "", "enterprise@example.com", "fpt@example.com");
            WriteAcademicHeader(academic);
            WriteAcademicRow(academic, 2, "academic@example.com", "Academic");
        }, reverse: true);

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Select(item => item.Email).Should().BeEquivalentTo("enterprise@example.com", "academic@example.com");
    }

    [Fact]
    public void Parse_DuplicateEmailAcrossSheets_MarksBothRowsInvalid()
    {
        var file = CreateWorkbook((enterprise, academic) =>
        {
            WriteEnterpriseHeader(enterprise);
            WriteEnterpriseRow(enterprise, 2, "Enterprise", "", "same@example.com", "");
            WriteAcademicHeader(academic);
            WriteAcademicRow(academic, 2, "same@example.com", "Academic");
        });

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Should().OnlyContain(item => !item.IsValid && item.Status == "Conflict");
    }

    [Fact]
    public void Parse_MissingRequiredSheet_ReturnsFailure()
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.AddWorksheet(MentorImportWorkbookParser.EnterpriseSheetName);
        WriteEnterpriseHeader(enterprise);
        WriteEnterpriseRow(enterprise, 2, "Enterprise", "", "enterprise@example.com", "");
        var file = ToFile(workbook);

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain(MentorImportWorkbookParser.AcademicSheetName);
    }

    private static IFormFile CreateWorkbook(Action<IXLWorksheet, IXLWorksheet> write, bool reverse = false)
    {
        using var workbook = new XLWorkbook();
        var firstName = reverse ? MentorImportWorkbookParser.AcademicSheetName : MentorImportWorkbookParser.EnterpriseSheetName;
        var secondName = reverse ? MentorImportWorkbookParser.EnterpriseSheetName : MentorImportWorkbookParser.AcademicSheetName;
        var first = workbook.AddWorksheet(firstName);
        var second = workbook.AddWorksheet(secondName);
        var enterprise = reverse ? second : first;
        var academic = reverse ? first : second;
        write(enterprise, academic);
        return ToFile(workbook);
    }

    private static IFormFile ToFile(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "mentors.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private static void WriteEnterpriseHeader(IXLWorksheet sheet)
    {
        string[] headers = ["STT", "Họ và tên", "Ngày tháng năm sinh", "SDT", "Loại HĐ", "Trình độ học vấn", "Địa chỉ hiện nay", "Email", "Fpt Email", "Vị trí, Chức danh", "Công ty"];
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
    }
    private static void WriteEnterpriseRow(IXLWorksheet sheet, int row, string name, string date, string email, string fptEmail)
    {
        sheet.Cell(row, 1).Value = row - 1; sheet.Cell(row, 2).Value = name; sheet.Cell(row, 3).Value = date;
        sheet.Cell(row, 4).Value = "0900000000"; sheet.Cell(row, 5).Value = "Thỉnh giảng"; sheet.Cell(row, 6).Value = "Thạc sĩ";
        sheet.Cell(row, 7).Value = "Đà Nẵng"; sheet.Cell(row, 8).Value = email; sheet.Cell(row, 9).Value = fptEmail;
        sheet.Cell(row, 10).Value = "CEO"; sheet.Cell(row, 11).Value = "Example Company";
    }
    private static void WriteAcademicHeader(IXLWorksheet sheet)
    {
        string[] headers = ["STT", "Email công việc", "Họ tên", "Phòng ban trực tiếp", "Chức danh (VN)"];
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
    }
    private static void WriteAcademicRow(IXLWorksheet sheet, int row, string email, string name)
    {
        sheet.Cell(row, 1).Value = row - 1; sheet.Cell(row, 2).Value = email; sheet.Cell(row, 3).Value = name;
        sheet.Cell(row, 4).Value = "Bộ môn CNTT"; sheet.Cell(row, 5).Value = "Giảng viên";
    }
}
