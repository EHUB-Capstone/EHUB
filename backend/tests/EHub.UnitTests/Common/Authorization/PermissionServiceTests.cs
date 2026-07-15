using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EHub.Application.Common.Services.Authorization;

namespace EHub.UnitTests.Common.Authorization;

public class PermissionServiceTests
{
    private readonly PermissionService _permissionService;

    public PermissionServiceTests()
    {
        _permissionService = new PermissionService();
    }

    [Fact]
    public async Task CanAccessProjectAsync_Should_Return_False_By_Default()
    {
        // Act
        var result = await _permissionService.CanAccessProjectAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanSubmitForTeamAsync_Should_Return_False_By_Default()
    {
        // Act
        var result = await _permissionService.CanSubmitForTeamAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanEvaluateSubmissionAsync_Should_Return_False_By_Default()
    {
        // Act
        var result = await _permissionService.CanEvaluateSubmissionAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanViewMentorAssignmentAsync_Should_Return_False_By_Default()
    {
        // Act
        var result = await _permissionService.CanViewMentorAssignmentAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}
