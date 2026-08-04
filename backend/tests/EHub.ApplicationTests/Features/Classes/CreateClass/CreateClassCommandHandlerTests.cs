using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.CreateClass;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.CreateClass;

public class CreateClassCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly CreateClassCommandHandler _handler;

    public CreateClassCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new CreateClassCommandHandler(_context);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WhenClassIndexIsInvalid_ReturnsValidationError(int invalidIndex)
    {
        // Arrange
        var request = new CreateClassRequest
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            ClassIndex = invalidIndex
        };

        // Act
        var result = await _handler.HandleAsync(request, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.InvalidClassIndex");
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Arrange
        var request = new CreateClassRequest
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            ClassIndex = 1
        };

        // Act
        var result = await _handler.HandleAsync(request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.AccessDenied");
    }
}
