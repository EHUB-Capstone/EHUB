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
        result.Value.Rows.Should().HaveCount(2);
        result.Value.Rows.Should().ContainSingle(item => item.MentorType == MentorType.Enterprise && item.FptEmail == null && item.DateOfBirth == new DateOnly(1993, 1, 21));
        result.Value.Rows.Should().ContainSingle(item => item.MentorType == MentorType.Academic && item.Email == "academic@example.edu.vn");
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
        result.Value.Rows.Select(item => item.Email).Should().BeEquivalentTo("enterprise@example.com", "academic@example.com");
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
        result.Value.Rows.Should().OnlyContain(item => !item.IsValid && item.Status == "Conflict");
    }

    [Fact]
    public void Parse_NameOnlyWorkbook_ReadsRowsAsValidIncompleteMentors()
    {
        var file = CreateWorkbook((enterprise, academic) =>
        {
            enterprise.Cell(1, 1).Value = "STT";
            enterprise.Cell(1, 2).Value = "Họ và tên";
            enterprise.Cell(2, 1).Value = 1;
            enterprise.Cell(2, 2).Value = "Mentor Doanh nghiệp Chưa Đủ";
            academic.Cell(1, 1).Value = "STT";
            academic.Cell(1, 2).Value = "Họ tên";
            academic.Cell(2, 1).Value = 1;
            academic.Cell(2, 2).Value = "Mentor Giảng viên Chưa Đủ";
        });

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Rows.Should().HaveCount(2);
        result.Value.Rows.Should().OnlyContain(item => item.IsValid && item.Email == string.Empty);
        result.Value.Rows.Should().ContainSingle(item => item.MentorType == MentorType.Enterprise && item.FullName == "Mentor Doanh nghiệp Chưa Đủ");
        result.Value.Rows.Should().ContainSingle(item => item.MentorType == MentorType.Academic && item.FullName == "Mentor Giảng viên Chưa Đủ");
    }

    [Fact]
    public void Parse_InvalidProvidedEmail_MarksRowInvalidInsteadOfSavingDraft()
    {
        var file = CreateWorkbook((enterprise, academic) =>
        {
            WriteEnterpriseHeader(enterprise);
            WriteEnterpriseRow(enterprise, 2, "Enterprise", "", "not-an-email", "");
            WriteAcademicHeader(academic);
            WriteAcademicRow(academic, 2, "academic@example.com", "Academic");
        });

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Rows.Single(item => item.MentorType == MentorType.Enterprise).IsValid.Should().BeFalse();
        result.Value.Rows.Single(item => item.MentorType == MentorType.Enterprise).Message.Should().Contain("valid email");
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

    [Theory]
    [InlineData(MentorType.Enterprise)]
    [InlineData(MentorType.Academic)]
    public void Parse_OneMentorTypeWithOtherSheetHeadersOnly_ReadsExactlyTheProvidedMentors(MentorType type)
    {
        var file = CreateWorkbook((enterprise, academic) =>
        {
            WriteEnterpriseHeader(enterprise);
            WriteAcademicHeader(academic);
            for (var index = 0; index < 30; index++)
            {
                var email = $"synthetic-mentor-{index}@example.test";
                if (type == MentorType.Enterprise)
                    WriteEnterpriseRow(enterprise, index + 2, $"Synthetic Enterprise {index}", "", email, "");
                else
                    WriteAcademicRow(academic, index + 2, email, $"Synthetic Academic {index}");
            }
        });

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Rows.Should().HaveCount(30).And.OnlyContain(row => row.IsValid && row.MentorType == type);
    }

    [Fact]
    public void Parse_BothSheetsContainOnlyHeaders_RejectsTheEmptyWorkbook()
    {
        var file = CreateWorkbook((enterprise, academic) =>
        {
            WriteEnterpriseHeader(enterprise);
            WriteAcademicHeader(academic);
            enterprise.Cell(2, 1).Value = 1;
            academic.Cell(2, 1).Value = 1;
        });

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("no mentor rows");
    }

    // A workbook whose mentor sheets carry the given header titles; each sheet gets one mentor row with the given cells.
    private static IFormFile CreateTitledWorkbook(string[] enterpriseHeaders, string[] enterpriseCells, string[] academicHeaders, string[] academicCells) =>
        CreateWorkbook((enterprise, academic) =>
        {
            for (var index = 0; index < enterpriseHeaders.Length; index++) enterprise.Cell(1, index + 1).Value = enterpriseHeaders[index];
            for (var index = 0; index < enterpriseCells.Length; index++) enterprise.Cell(2, index + 1).Value = enterpriseCells[index];
            for (var index = 0; index < academicHeaders.Length; index++) academic.Cell(1, index + 1).Value = academicHeaders[index];
            for (var index = 0; index < academicCells.Length; index++) academic.Cell(2, index + 1).Value = academicCells[index];
        });

    [Theory]
    [InlineData("FPT email")]
    [InlineData("FPT Email")]
    [InlineData("Email FPT")]
    [InlineData("Mail FPT")]
    [InlineData("Email (FPT)")]
    public void Parse_FptEmailTitle_IsUsedAsTheLoginEmail_WhenThereIsNoEmailColumn(string title)
    {
        var file = CreateTitledWorkbook(
            ["STT", "Họ và tên", title], ["1", "Enterprise Mentor", "enterprise@fpt.edu.vn"],
            ["STT", "Họ tên", title], ["1", "Lecturer Mentor", "lecturer@fpt.edu.vn"]);

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Rows.Single(item => item.MentorType == MentorType.Enterprise).Email.Should().Be("enterprise@fpt.edu.vn");
        result.Value.Rows.Single(item => item.MentorType == MentorType.Academic).Email.Should().Be("lecturer@fpt.edu.vn");
        result.Value.Warnings.Should().ContainSingle(item => item.Contains("FPT email is used as the login email"));
    }

    [Fact]
    public void Parse_EnterpriseEmailColumn_WinsOverTheFptEmail_AndTheFptEmailIsOnlyAFallbackForBlankCells()
    {
        var file = CreateTitledWorkbook(
            ["STT", "Họ và tên", "Email", "Fpt Email"], ["1", "Has Both", "personal@example.com", "work@fpt.edu.vn"],
            ["STT", "Họ tên", "Email công việc"], ["1", "Lecturer", "lecturer@fpt.edu.vn"]);
        var blankEmail = MentorImportWorkbookParser.Parse(CreateWorkbook((enterprise, academic) =>
        {
            enterprise.Cell(1, 1).Value = "STT"; enterprise.Cell(1, 2).Value = "Họ và tên"; enterprise.Cell(1, 3).Value = "Email"; enterprise.Cell(1, 4).Value = "Fpt Email";
            enterprise.Cell(2, 1).Value = 1; enterprise.Cell(2, 2).Value = "Blank Email"; enterprise.Cell(2, 4).Value = "fallback@fpt.edu.vn";
            academic.Cell(1, 1).Value = "STT"; academic.Cell(1, 2).Value = "Họ tên"; academic.Cell(2, 1).Value = 1; academic.Cell(2, 2).Value = "Lecturer";
        }));

        var both = MentorImportWorkbookParser.Parse(file);

        var kept = both.Value.Rows.Single(item => item.MentorType == MentorType.Enterprise);
        kept.Email.Should().Be("personal@example.com");
        kept.FptEmail.Should().Be("work@fpt.edu.vn");
        both.Value.Warnings.Should().BeEmpty();
        blankEmail.Value.Rows.Single(item => item.MentorType == MentorType.Enterprise).Email.Should().Be("fallback@fpt.edu.vn");
    }

    [Fact]
    public void Parse_ColumnsThatAreNotRecognised_AreNamedInAWarning_InsteadOfBeingIgnoredSilently()
    {
        var file = CreateTitledWorkbook(
            ["STT", "Họ và tên", "Email", "Ghi chú nội bộ"], ["1", "Enterprise", "e@example.com", "note"],
            ["STT", "Họ tên", "Email công việc", "Mã nhân viên"], ["1", "Lecturer", "l@example.com", "123"]);

        var result = MentorImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Warnings.Should().Contain(item => item.Contains(MentorImportWorkbookParser.EnterpriseSheetName) && item.Contains("Ghi chú nội bộ"));
        result.Value.Warnings.Should().Contain(item => item.Contains(MentorImportWorkbookParser.AcademicSheetName) && item.Contains("Mã nhân viên"));
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
