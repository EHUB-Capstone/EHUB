using ClosedXML.Excel;
using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.Classes.ImportSemesterGroups;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ImportSemesterGroups;

public sealed class SemesterGroupImportWorkbookParserTests
{
    [Fact]
    public async Task PreviewAsync_WhenUserIsStudent_ReturnsAccessDeniedBeforeReadingTheFile()
    {
        var handler = new ImportSemesterGroupsCommandHandler(Substitute.For<IApplicationDbContext>());

        var result = await handler.PreviewAsync(
            Guid.NewGuid(),
            null!,
            Guid.NewGuid(),
            SystemRoles.Student);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Theory]
    [InlineData("FA2026", "Group FA26")]
    [InlineData("SP2027", "Group SP27")]
    [InlineData("su2026", "Group SU26")]
    public void GetHeader_UsesSemesterLettersAndLastTwoYearDigits(string semesterCode, string expected)
    {
        SemesterGroupColumn.GetHeader(semesterCode).Should().Be(expected);
    }

    [Fact]
    public void Parse_WithExpectedDynamicColumn_ReadsRollNumberAndGroup()
    {
        using var stream = CreateWorkbook(" group fa26 ", "DE180182", "EXE201g_8G1");
        var file = CreateFormFile(stream);

        var result = SemesterGroupImportWorkbookParser.Parse(file, "Group FA26");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Should().Be(new SemesterGroupSourceRow(2, "DE180182", "EXE201g_8G1"));
    }

    [Fact]
    public void Parse_WithDifferentSemesterColumn_ReturnsClearFailure()
    {
        using var stream = CreateWorkbook("Group SP27", "DE180182", "EXE201g_8G1");
        var file = CreateFormFile(stream);

        var result = SemesterGroupImportWorkbookParser.Parse(file, "Group FA26");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Group FA26");
    }

    private static MemoryStream CreateWorkbook(string groupHeader, string rollNumber, string groupName)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Class Roster");
            worksheet.Cell(1, 1).Value = "RollNumber";
            worksheet.Cell(1, 2).Value = groupHeader;
            worksheet.Cell(2, 1).Value = rollNumber;
            worksheet.Cell(2, 2).Value = groupName;
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }

    private static FormFile CreateFormFile(Stream stream) =>
        new(stream, 0, stream.Length, "file", "semester-groups.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
}
