using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.UpdateClass;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.UpdateClass;

public class UpdateClassCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateClassCommandHandler _handler;

    public UpdateClassCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new UpdateClassCommandHandler(_context, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Arrange
        var request = new UpdateClassRequest
        {
            Room = "BE-201"
        };

        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task UpdateTeachingAssignmentAsync_WhenUserIsLecturer_ReturnsAccessDeniedError()
    {
        // Arrange
        var request = new UpdateTeachingAssignmentRequest
        {
            PrimaryLecturerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.UpdateTeachingAssignmentAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Lecturer);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task UpdateTeachingAssignmentAsync_WhenLecturerPropertyIsMissing_ReturnsValidationError()
    {
        var request = new UpdateTeachingAssignmentRequest { RowVersion = "1" };

        var result = await _handler.UpdateTeachingAssignmentAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public async Task HandleAsync_WhenRoomPropertyIsMissing_ReturnsValidationError()
    {
        var request = new UpdateClassRequest { RowVersion = "1" };

        var result = await _handler.HandleAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }
}
