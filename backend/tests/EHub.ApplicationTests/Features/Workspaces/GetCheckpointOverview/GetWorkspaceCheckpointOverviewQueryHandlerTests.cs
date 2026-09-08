using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Workspaces.GetCheckpointOverview;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Workspaces.GetCheckpointOverview;

public sealed class GetWorkspaceCheckpointOverviewQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenRoleCannotAccessWorkspaces_ReturnsAccessDenied()
    {
        var handler = new GetWorkspaceCheckpointOverviewQueryHandler(
            Substitute.For<IApplicationDbContext>());

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UNKNOWN");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }
}
