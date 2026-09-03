using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.AssignStudents;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Classes.AssignStudents;

public sealed class AssignStudentsCommandHandlerTests
{
    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task AssignToClassAsync_WhenNoStudentsSelected_ReturnsValidationErrorBeforeAccessingData()
    {
        var handler = new AssignStudentsCommandHandler(_context, _unitOfWork);

        var result = await handler.AssignToClassAsync(
            Guid.NewGuid(),
            new AssignStudentsToClassRequest(),
            Guid.NewGuid(),
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAssignmentStudentsRequired);
    }

    [Fact]
    public async Task AssignToTeamAsync_WhenNoStudentsSelected_ReturnsValidationErrorBeforeAccessingData()
    {
        var handler = new AssignStudentsCommandHandler(_context, _unitOfWork);

        var result = await handler.AssignToTeamAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AssignStudentsToTeamRequest(),
            Guid.NewGuid(),
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamAssignmentStudentsRequired);
    }
}
