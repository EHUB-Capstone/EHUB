using ClosedXML.Excel;
using EHub.Application.Features.StartupIndustries.ImportStartupIndustries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace EHub.ApplicationTests.Features.StartupIndustries.ImportStartupIndustries;

public sealed class StartupIndustryImportWorkbookParserTests
{
    [Theory]
    [InlineData("Industry")]
    [InlineData("Industry name")]
    public void Parse_ValidWorkbook_AcceptsSupportedIndustryHeaders(string industryHeader)
    {
        var file = CreateWorkbook(worksheet =>
        {
            worksheet.Cell(1, 1).Value = industryHeader;
            worksheet.Cell(1, 2).Value = "Description";
            worksheet.Cell(2, 1).Value = "  Clean Energy  ";
            worksheet.Cell(2, 2).Value = "  Renewable energy solutions.  ";
        });

        var result = StartupIndustryImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Name.Should().Be("Clean Energy");
        result.Value[0].Description.Should().Be("Renewable energy solutions.");
        result.Value[0].NormalizedName.Should().Be("CLEAN ENERGY");
    }

    [Fact]
    public void Parse_DuplicateIndustryNames_MarksEveryDuplicateInvalid()
    {
        var file = CreateWorkbook(worksheet =>
        {
            WriteHeader(worksheet);
            worksheet.Cell(2, 1).Value = "HealthTech";
            worksheet.Cell(3, 1).Value = " healthtech ";
        });

        var result = StartupIndustryImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2).And.OnlyContain(row => !row.IsValid);
        result.Value.Should().OnlyContain(row => row.ErrorMessage!.Contains("appears more than once"));
    }

    [Fact]
    public void Parse_MissingDescriptionHeader_ReturnsFileError()
    {
        var file = CreateWorkbook(worksheet =>
        {
            worksheet.Cell(1, 1).Value = "Industry name";
            worksheet.Cell(2, 1).Value = "EdTech";
        });

        var result = StartupIndustryImportWorkbookParser.Parse(file);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Description");
    }

    [Fact]
    public void Parse_DescriptionOverLimit_ReturnsRowError()
    {
        var file = CreateWorkbook(worksheet =>
        {
            WriteHeader(worksheet);
            worksheet.Cell(2, 1).Value = "EdTech";
            worksheet.Cell(2, 2).Value = new string('a', 241);
        });

        var result = StartupIndustryImportWorkbookParser.Parse(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].IsValid.Should().BeFalse();
        result.Value[0].ErrorMessage.Should().Contain("240");
    }

    private static IFormFile CreateWorkbook(Action<IXLWorksheet> write)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Sheet1");
        write(worksheet);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "industries.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private static void WriteHeader(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "Industry name";
        worksheet.Cell(1, 2).Value = "Description";
    }
}
