using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.AddStudentToClass;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.AddStudentToClass;

public class AddStudentToClassCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly AddStudentToClassCommandHandler _handler;

    public AddStudentToClassCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new AddStudentToClassCommandHandler(_context);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Arrange
        var request = new AddStudentToClassRequest
        {
            StudentCode = "SE170001",
            FullName = "Nguyen Van A",
            Email = "anv@fpt.edu.vn",
            MajorCode = "BIT_SE"
        };

        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.AccessDenied");
    }

    [Theory]
    [InlineData("", "Nguyen Van A", "anv@fpt.edu.vn", "BIT_SE", "Classes.InvalidStudentCode")]
    [InlineData("SE170001", "", "anv@fpt.edu.vn", "BIT_SE", "Classes.InvalidFullName")]
    [InlineData("SE170001", "Nguyen Van A", "invalid-email", "BIT_SE", "Classes.InvalidEmail")]
    [InlineData("SE170001", "Nguyen Van A", "anv@fpt.edu.vn", "INVALID_MAJOR", "Classes.InvalidMajorCode")]
    public async Task HandleAsync_WhenValidationFails_ReturnsExpectedErrorCode(
        string code, string name, string email, string major, string expectedErrorCode)
    {
        // Arrange
        var request = new AddStudentToClassRequest
        {
            StudentCode = code,
            FullName = name,
            Email = email,
            MajorCode = major
        };

        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }
}
