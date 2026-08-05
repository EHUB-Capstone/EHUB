using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Teams.MentorAssignments;
using EHub.Application.Features.Teams.ProjectDirections;
using EHub.Application.Features.Teams.TeamProposals;
using EHub.Contracts.Teams;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Teams;

public sealed class TeamWorkflowAuthorizationTests
{
    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task LecturerCannotAssignMentor()
    {
        var handler = new MentorAssignmentHandler(_context, _unitOfWork);

        var result = await handler.AssignAsync(
            Guid.NewGuid(),
            new AssignMentorRequest { MentorProfileId = Guid.NewGuid() },
            Guid.NewGuid(),
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task LecturerCannotCreateStudentTeamProposal()
    {
        var handler = new TeamProposalHandler(_context, _unitOfWork);

        var result = await handler.CreateAsync(
            Guid.NewGuid(),
            new CreateTeamProposalRequest(),
            Guid.NewGuid(),
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task AdminCannotReviewProjectDirectionOnBehalfOfAssignedLecturer()
    {
        var handler = new ProjectDirectionHandler(_context);

        var result = await handler.ReviewAsync(
            Guid.NewGuid(),
            new ReviewProjectDirectionRequest
            {
                Decision = "Approved",
                Comment = "Approved for implementation.",
                RowVersion = "1"
            },
            Guid.NewGuid(),
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }
}
