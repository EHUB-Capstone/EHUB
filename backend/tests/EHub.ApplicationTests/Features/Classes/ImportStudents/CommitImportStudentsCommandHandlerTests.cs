using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ImportStudents;

public class CommitImportStudentsCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommitImportStudentsCommandHandler _handler;

    public CommitImportStudentsCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CommitImportStudentsCommandHandler(_context, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Act
        var request = new CommitImportStudentsRequest { SessionId = Guid.NewGuid() };
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenLecturerUsesEmptySessionId_ReturnsValidationError()
    {
        var request = new CommitImportStudentsRequest { SessionId = Guid.Empty };
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Lecturer);

        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }
}
