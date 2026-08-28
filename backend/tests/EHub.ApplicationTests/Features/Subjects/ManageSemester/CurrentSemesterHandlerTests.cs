using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Subjects.ManageSemester;
using EHub.Contracts.Subjects;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Subjects.ManageSemester;

public sealed class CurrentSemesterHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly CurrentSemesterHandler _handler;

    public CurrentSemesterHandlerTests()
    {
        _handler = new CurrentSemesterHandler(
            Substitute.For<IApplicationDbContext>(),
            _currentUser,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<ILogger<CurrentSemesterHandler>>());
    }

    [Fact]
    public async Task CorrectAsync_WhenCallerIsNotAdmin_ReturnsAccessDenied()
    {
        _currentUser.Roles.Returns(new[] { SystemRoles.Lecturer });

        var result = await _handler.CorrectAsync(ValidRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task CorrectAsync_WhenSemesterIdentifiersAreInvalid_ReturnsValidationError()
    {
        _currentUser.Roles.Returns(new[] { SystemRoles.Admin });
        var semesterId = Guid.NewGuid();

        var missingSemester = await _handler.CorrectAsync(new CorrectActiveSemesterRequest
        {
            CurrentSemesterId = Guid.Empty,
            CurrentRowVersion = "1",
            TargetSemesterId = Guid.NewGuid(),
            TargetRowVersion = "2",
            Reason = "Correct invalid active semester"
        });
        var sameSemester = await _handler.CorrectAsync(new CorrectActiveSemesterRequest
        {
            CurrentSemesterId = semesterId,
            CurrentRowVersion = "1",
            TargetSemesterId = semesterId,
            TargetRowVersion = "2",
            Reason = "Correct invalid active semester"
        });

        missingSemester.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        sameSemester.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public async Task CorrectAsync_WhenVersionsOrReasonAreInvalid_ReturnsValidationError()
    {
        _currentUser.Roles.Returns(new[] { SystemRoles.Admin });
        var invalidVersion = new CorrectActiveSemesterRequest
        {
            CurrentSemesterId = Guid.NewGuid(),
            CurrentRowVersion = "stale",
            TargetSemesterId = Guid.NewGuid(),
            TargetRowVersion = "2",
            Reason = "Correct invalid active semester"
        };
        var invalidReason = new CorrectActiveSemesterRequest
        {
            CurrentSemesterId = Guid.NewGuid(),
            CurrentRowVersion = "1",
            TargetSemesterId = Guid.NewGuid(),
            TargetRowVersion = "2",
            Reason = "x"
        };

        var versionResult = await _handler.CorrectAsync(invalidVersion);
        var reasonResult = await _handler.CorrectAsync(invalidReason);

        versionResult.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        reasonResult.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    private static CorrectActiveSemesterRequest ValidRequest() => new()
    {
        CurrentSemesterId = Guid.NewGuid(),
        CurrentRowVersion = "1",
        TargetSemesterId = Guid.NewGuid(),
        TargetRowVersion = "2",
        Reason = "Correct invalid active semester"
    };
}
