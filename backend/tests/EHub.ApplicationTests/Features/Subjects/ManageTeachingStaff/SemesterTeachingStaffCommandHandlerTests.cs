using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Subjects.ManageTeachingStaff;
using EHub.Contracts.Subjects;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Subjects.ManageTeachingStaff;

public sealed class SemesterTeachingStaffCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly SemesterTeachingStaffCommandHandler _handler;

    public SemesterTeachingStaffCommandHandlerTests()
    {
        _handler = new SemesterTeachingStaffCommandHandler(
            Substitute.For<IApplicationDbContext>(),
            _currentUser,
            Substitute.For<IUnitOfWork>());
    }

    [Fact]
    public async Task AddAsync_WhenCallerIsNotAdmin_ReturnsAccessDenied()
    {
        _currentUser.Roles.Returns(new[] { SystemRoles.Lecturer });

        var result = await _handler.AddAsync(new AddSemesterTeachingStaffRequest
        {
            Semester = "FA",
            Year = DateTime.UtcNow.Year,
            UserId = Guid.NewGuid(),
            Role = "LECTURER"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task AddAsync_WhenSemesterOrRoleIsInvalid_ReturnsValidationError()
    {
        _currentUser.Roles.Returns(new[] { SystemRoles.Admin });

        var invalidSemester = await _handler.AddAsync(new AddSemesterTeachingStaffRequest
        {
            Semester = "WINTER",
            Year = DateTime.UtcNow.Year,
            UserId = Guid.NewGuid(),
            Role = "LECTURER"
        });
        var invalidRole = await _handler.AddAsync(new AddSemesterTeachingStaffRequest
        {
            Semester = "FA",
            Year = DateTime.UtcNow.Year,
            UserId = Guid.NewGuid(),
            Role = "ADMIN"
        });

        invalidSemester.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        invalidRole.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public async Task UpdateAsync_WhenVersionOrStatusIsInvalid_ReturnsValidationError()
    {
        _currentUser.Roles.Returns(new[] { SystemRoles.Admin });

        var invalidVersion = await _handler.UpdateAsync(
            Guid.NewGuid(),
            new UpdateSemesterTeachingStaffRequest
            {
                Status = "Inactive",
                RowVersion = "stale"
            });
        var invalidStatus = await _handler.UpdateAsync(
            Guid.NewGuid(),
            new UpdateSemesterTeachingStaffRequest
            {
                Status = "Removed",
                RowVersion = "1"
            });

        invalidVersion.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        invalidStatus.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }
}
