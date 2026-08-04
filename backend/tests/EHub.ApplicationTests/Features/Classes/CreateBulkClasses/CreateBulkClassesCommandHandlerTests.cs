using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.CreateBulkClasses;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.CreateBulkClasses;

public class CreateBulkClassesCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly CreateBulkClassesCommandHandler _handler;

    public CreateBulkClassesCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new CreateBulkClassesCommandHandler(_context);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task PreviewAsync_WhenQuantityIsInvalid_ReturnsValidationError(int invalidQuantity)
    {
        // Arrange
        var request = new CreateBulkClassesRequest
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StartClassIndex = 1,
            Quantity = invalidQuantity
        };

        // Act
        var result = await _handler.PreviewAsync(request, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.InvalidQuantity");
    }

    [Fact]
    public async Task PreviewAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Arrange
        var request = new CreateBulkClassesRequest
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StartClassIndex = 1,
            Quantity = 5
        };

        // Act
        var result = await _handler.PreviewAsync(request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.AccessDenied");
    }
}
