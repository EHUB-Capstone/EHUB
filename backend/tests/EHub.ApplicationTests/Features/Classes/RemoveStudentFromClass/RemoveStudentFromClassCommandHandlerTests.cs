using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.RemoveStudentFromClass;
using EHub.Shared.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.RemoveStudentFromClass;

public class RemoveStudentFromClassCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly RemoveStudentFromClassCommandHandler _handler;

    public RemoveStudentFromClassCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new RemoveStudentFromClassCommandHandler(_context);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.AccessDenied");
    }
}
