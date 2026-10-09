using EHub.Application.Features.Classes.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ImportStudents;

public sealed class ExcelWorkbookSecurityTests
{
    [Fact]
    public void Validate_WhenExtensionIsXlsxButSignatureIsOle_ReturnsSignatureError()
    {
        var file = CreateFile(
            [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1],
            "students.xlsx",
            "application/octet-stream");

        var result = ExcelWorkbookSecurity.Validate(file);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.InvalidFileSignature");
    }

    [Fact]
    public void Validate_WhenExtensionAndSignatureAreValidButMimeIsPdf_ReturnsMimeError()
    {
        var file = CreateFile(
            [0x50, 0x4B, 0x03, 0x04],
            "students.xlsx",
            "application/pdf");

        var result = ExcelWorkbookSecurity.Validate(file);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.InvalidFileMimeType");
    }

    [Theory]
    [InlineData("students.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", true)]
    [InlineData("students.xls", "application/vnd.ms-excel", false)]
    public void Validate_WhenExtensionMimeAndSignatureMatch_ReturnsWorkbookKind(
        string fileName,
        string mimeType,
        bool openXml)
    {
        var bytes = openXml
            ? new byte[] { 0x50, 0x4B, 0x03, 0x04 }
            : new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        var file = CreateFile(bytes, fileName, mimeType);

        var result = ExcelWorkbookSecurity.Validate(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(openXml ? ExcelWorkbookKind.OpenXml : ExcelWorkbookKind.Binary);
    }

    private static IFormFile CreateFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
