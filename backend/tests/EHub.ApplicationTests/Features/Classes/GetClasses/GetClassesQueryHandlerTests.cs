using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.GetClasses;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.GetClasses;

public class GetClassesQueryHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly GetClassesQueryHandler _handler;

    public GetClassesQueryHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new GetClassesQueryHandler(_context);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WhenPageIsInvalid_ReturnsValidationError(int invalidPage)
    {
        // Arrange
        var request = new GetClassesRequest { Page = invalidPage };

        // Act
        var result = await _handler.HandleAsync(request, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task HandleAsync_WhenPageSizeIsInvalid_ReturnsValidationError(int invalidPageSize)
    {
        // Arrange
        var request = new GetClassesRequest { PageSize = invalidPageSize };

        // Act
        var result = await _handler.HandleAsync(request, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsUnauthorizedRole_ReturnsAccessDeniedError()
    {
        // Arrange
        var request = new GetClassesRequest();

        // Act
        var result = await _handler.HandleAsync(request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenAssignmentStatusIsInvalid_ReturnsValidationError()
    {
        var request = new GetClassesRequest { AssignmentStatus = "Pending" };

        var result = await _handler.HandleAsync(request, Guid.NewGuid(), SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        result.Error.Message.Should().Be("Assignment status must be Assigned or Unassigned.");
    }
}
