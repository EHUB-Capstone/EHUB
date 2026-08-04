using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
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
    private readonly IImportSessionStore _sessionStore;
    private readonly PreviewImportStudentsCommandHandler _handler;

    public PreviewImportStudentsCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _sessionStore = Substitute.For<IImportSessionStore>();
        _handler = new PreviewImportStudentsCommandHandler(_context, _sessionStore);
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
    public async Task HandleAsync_WhenUserIsLecturer_ReturnsAccessDeniedDuringSafetyHardening()
    {
        var result = await _handler.HandleAsync(Guid.NewGuid(), null!, Guid.NewGuid(), SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
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
}
