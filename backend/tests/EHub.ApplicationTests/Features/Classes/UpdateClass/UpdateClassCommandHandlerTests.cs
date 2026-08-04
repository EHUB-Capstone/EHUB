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
    private readonly UpdateClassCommandHandler _handler;

    public UpdateClassCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new UpdateClassCommandHandler(_context);
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
}
