using EHub.Application.Features.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Workspaces;

public sealed class ProjectWorkspaceApprovalRulesTests
{
    [Theory]
    [InlineData(ProjectDirectionStatus.Draft, false)]
    [InlineData(ProjectDirectionStatus.Submitted, false)]
    [InlineData(ProjectDirectionStatus.NeedsRevision, false)]
    [InlineData(ProjectDirectionStatus.Approved, true)]
    public void InitialProposal_RequiresApproval(ProjectDirectionStatus status, bool expected) =>
        ProjectWorkspaceApprovalRules.IsApproved(new ProjectDirection { Status = status }).Should().Be(expected);

    [Theory]
    [InlineData(ProjectDirectionStatus.Draft)]
    [InlineData(ProjectDirectionStatus.Submitted)]
    [InlineData(ProjectDirectionStatus.NeedsRevision)]
    public void ApprovedProfileChange_KeepsWorkspaceAvailable(ProjectDirectionStatus status)
    {
        var direction = new ProjectDirection { Status = status };
        direction.Reviews.Add(new ProjectDirectionReview { ToStatus = ProjectDirectionStatus.Approved });
        ProjectWorkspaceApprovalRules.IsApproved(direction).Should().BeTrue();
    }

    [Fact]
    public void LegacyProjectWithoutDirection_KeepsExistingAccess() =>
        ProjectWorkspaceApprovalRules.IsApproved(null).Should().BeTrue();
}
