using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ImportStudents;

public class PreviewImportStudentsCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly PreviewImportStudentsCommandHandler _handler;

    public PreviewImportStudentsCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new PreviewImportStudentsCommandHandler(_context);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), null!, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsLecturerAndFileIsNull_ReachesFileValidation()
    {
        var result = await _handler.HandleAsync(Guid.NewGuid(), null!, Guid.NewGuid(), SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.FileEmpty");
    }

    [Fact]
    public async Task HandleAsync_WhenFileIsNull_ReturnsFileEmptyError()
    {
        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), null!, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.FileEmpty");
    }

    [Fact]
    public async Task HandleAsync_WhenFileHasInvalidExtension_ReturnsInvalidFileTypeError()
    {
        // Arrange
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(1024);
        file.FileName.Returns("test.pdf");

        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), file, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.InvalidFileType");
    }

    [Fact]
    public void ParseWorkbook_WhenFileIsLegacyXls_ReadsStudentRows()
    {
        const string legacyXlsBase64 = "0M8R4KGxGuEAAAAAAAAAAAAAAAAAAAAAPgADAP7/CQAGAAAAAAAAAAAAAAABAAAAAgAAAAAAAAAAEAAAAQAAAAEAAAD+////AAAAAAAAAAD////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////9/////v////7///8EAAAABQAAAP7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7///8CAAAAAwAAAAQAAAAFAAAABgAAAAcAAAAIAAAACQAAAAoAAAALAAAADAAAAA0AAAAOAAAADwAAABAAAAARAAAAEgAAABMAAAAUAAAAFQAAAP7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+/////v////7////+////UgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABQH//////////wEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAAAAgAUAAAAAAAABAFMAaAAzADMAdABKADUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEgACAf////8CAAAA/////wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAFcAbwByAGsA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";
        var bytes = Convert.FromBase64String(legacyXlsBase64);
        using var stream = new MemoryStream(bytes);
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "students.xls");

        var result = PreviewImportStudentsCommandHandler.ParseWorkbook(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].RowNumber.Should().Be(2);
        result.Value[0].StudentCode.Should().Be("SE765432");
        result.Value[0].FullName.Should().Be("Nguyen Van X");
        result.Value[0].Email.Should().Be("xls.student@fpt.edu.vn");
        result.Value[0].MajorCode.Should().Be("BIT_SE");
        result.Value[0].IsValid.Should().BeTrue();
    }

    [Fact]
    public void ParseWorkbook_WhenXlsExtensionContainsSpreadsheetMl_ReadsLegacyRosterWithoutMajor()
    {
        const string spreadsheetMl = """
            <?xml version="1.0"?>
            <?mso-application progid="Excel.Sheet"?>
            <Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"
                      xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">
              <Worksheet ss:Name="Sheet1">
                <Table>
                  <Row>
                    <Cell><Data ss:Type="String">Class</Data></Cell>
                    <Cell><Data ss:Type="String">RollNumber</Data></Cell>
                    <Cell><Data ss:Type="String">Email</Data></Cell>
                    <Cell><Data ss:Type="String">MemberCode</Data></Cell>
                    <Cell><Data ss:Type="String">FullName</Data></Cell>
                  </Row>
                  <Row>
                    <Cell><Data ss:Type="String">EXE101_8</Data></Cell>
                    <Cell><Data ss:Type="String">DE180225</Data></Cell>
                    <Cell><Data ss:Type="String">LinhTNDE180225@fpt.edu.vn</Data></Cell>
                    <Cell><Data ss:Type="String">LinhTNDE180225</Data></Cell>
                    <Cell><Data ss:Type="String">Thái Ngọc Linh</Data></Cell>
                  </Row>
                </Table>
              </Worksheet>
            </Workbook>
            """;
        var bytes = System.Text.Encoding.UTF8.GetBytes(spreadsheetMl);
        using var stream = new MemoryStream(bytes);
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "Import tạo lớp.xls");

        var result = PreviewImportStudentsCommandHandler.ParseWorkbook(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].RowNumber.Should().Be(2);
        result.Value[0].StudentCode.Should().Be("DE180225");
        result.Value[0].FullName.Should().Be("Thái Ngọc Linh");
        result.Value[0].Email.Should().Be("linhtnde180225@fpt.edu.vn");
        result.Value[0].MajorCode.Should().Be(MajorCodes.Undeclared);
        result.Value[0].IsValid.Should().BeTrue();
    }

}
