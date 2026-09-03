using ClosedXML.Excel;
using EHub.Application.Features.Admin.Users.ImportLecturers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace EHub.ApplicationTests.Features.Admin.Users.ImportLecturers;

public sealed class LecturerImportWorkbookParserTests
{
    [Fact]
    public void Parse_LegacyLecturerLayout_PrefersBlankHeaderColumnBeforeRoles()
    {
        var file = CreateWorkbook(worksheet =>
        {
            WriteLegacyHeader(worksheet);
            WriteRow(worksheet, 2, "Lecturer A", "Head", "contact@example.org", "google@example.net", "Lecturer");
        });

        var result = LecturerImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].GoogleEmail.Should().Be("google@example.net");
        result.Value[0].ContactEmail.Should().Be("contact@example.org");
        result.Value[0].IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WhenPreferredColumnIsEmpty_FallsBackToNamedEmailColumn()
    {
        var file = CreateWorkbook(worksheet =>
        {
            WriteLegacyHeader(worksheet);
            WriteRow(worksheet, 2, "Lecturer B", "Lecturer", "fallback@example.com", string.Empty, "Lecturer");
        });

        var result = LecturerImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].GoogleEmail.Should().Be("fallback@example.com");
        result.Value[0].IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_DoesNotRestrictEmailDomain()
    {
        var file = CreateWorkbook(worksheet =>
        {
            WriteLegacyHeader(worksheet);
            WriteRow(worksheet, 2, "Lecturer C", "Lecturer", "lecturer@independent.edu", string.Empty, "Lecturer");
        });

        var result = LecturerImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].IsValid.Should().BeTrue();
        result.Value[0].GoogleEmail.Should().Be("lecturer@independent.edu");
    }

    [Fact]
    public void Parse_WhenLoginEmailIsDuplicated_MarksEveryDuplicateInvalid()
    {
        var file = CreateWorkbook(worksheet =>
        {
            WriteLegacyHeader(worksheet);
            WriteRow(worksheet, 2, "Lecturer D", "Lecturer", "first@example.org", "duplicate@example.org", "Lecturer");
            WriteRow(worksheet, 3, "Lecturer E", "Lecturer", "second@example.org", "duplicate@example.org", "Lecturer");
        });

        var result = LecturerImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2).And.OnlyContain(row => !row.IsValid && row.Status == "Invalid");
    }

    [Fact]
    public void Parse_WhenRoleIsNotLecturer_MarksRowInvalid()
    {
        var file = CreateWorkbook(worksheet =>
        {
            WriteLegacyHeader(worksheet);
            WriteRow(worksheet, 2, "Not Lecturer", "Staff", "staff@example.org", string.Empty, "Admin");
        });

        var result = LecturerImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].IsValid.Should().BeFalse();
        result.Value[0].Message.Should().Contain("Lecturer");
    }

    private static IFormFile CreateWorkbook(Action<IXLWorksheet> write)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Sheet1");
        write(worksheet);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "lecturers.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private static void WriteLegacyHeader(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "STT";
        worksheet.Cell(1, 2).Value = "Tên Giảng Viên";
        worksheet.Cell(1, 3).Value = "Vị trí";
        worksheet.Cell(1, 4).Value = "Email";
        worksheet.Cell(1, 6).Value = "Roles";
    }

    private static void WriteRow(
        IXLWorksheet worksheet,
        int row,
        string name,
        string position,
        string email,
        string googleEmail,
        string role)
    {
        worksheet.Cell(row, 1).Value = row - 1;
        worksheet.Cell(row, 2).Value = name;
        worksheet.Cell(row, 3).Value = position;
        worksheet.Cell(row, 4).Value = email;
        worksheet.Cell(row, 5).Value = googleEmail;
        worksheet.Cell(row, 6).Value = role;
    }
}
