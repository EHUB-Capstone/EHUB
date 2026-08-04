using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ImportStudents;

public class CommitImportStudentsCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly IImportSessionStore _sessionStore;
    private readonly CommitImportStudentsCommandHandler _handler;

    public CommitImportStudentsCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _sessionStore = Substitute.For<IImportSessionStore>();
        _handler = new CommitImportStudentsCommandHandler(_context, _sessionStore);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Act
        var request = new CommitImportStudentsRequest { SessionId = Guid.NewGuid() };
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.AccessDenied");
    }

    [Fact]
    public async Task HandleAsync_WhenSessionExpiredOrProcessed_ReturnsExpiredError()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        (Guid ClassId, Guid UserId, List<ImportStudentRowPreviewDto> ValidRows)? nullSession = null;
        _sessionStore.GetAndConsumeSession(sessionId).Returns(nullSession);

        var request = new CommitImportStudentsRequest { SessionId = sessionId };

        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.ImportSessionExpiredOrProcessed");
    }
}
