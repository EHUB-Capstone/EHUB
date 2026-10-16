using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Teams.ProjectDirections;
using EHub.Application.Features.Workspaces;
using EHub.Contracts.Teams;
using EHub.Contracts.Workspaces;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

public sealed partial class TeamWorkflowIntegrationTests
{
    private const string ProfileName = "Campus Circular";
    private const string ProfileDescription = "A student marketplace that helps campuses reuse equipment safely.";
    private const string ProfileZaloUrl = "https://zalo.me/g/campus-circular";

    [Fact]
    public async Task ProfileIndustryChange_IsSentForLecturerReview_AndTheApprovedIndustriesStayUntilApproval()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (seed, workspace, directions) = await CreateApprovedProfileAsync(scope, context);

        // Only the startup industries change: two industries become one.
        var update = await workspace.UpdateAsync(seed.TeamId!.Value, ProfileRequest([seed.StartupIndustryIds[0]]),
            seed.ProposerUserId, SystemRoles.Student);

        update.IsSuccess.Should().BeTrue($"{update.Error?.Code}: {update.Error?.Message}");
        var direction = (await directions.GetAsync(seed.TeamId.Value, seed.ProposerUserId, SystemRoles.Student)).Value;
        direction.Status.Should().Be(nameof(ProjectDirectionStatus.Submitted));
        direction.IsProjectProfileChangeProposal.Should().BeTrue();
        direction.ProposedStartupIndustries.Should().Equal(await IndustryNamesAsync(context, seed.StartupIndustryIds[0]));
        direction.StartupIndustries.Should().BeEquivalentTo(await IndustryNamesAsync(context, seed.StartupIndustryIds[0], seed.StartupIndustryIds[1]),
            "the project keeps its approved industries until the lecturer approves");
    }

    [Fact]
    public async Task ProfileIndustryChange_WhenApproved_ReplacesTheProjectIndustries()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (seed, workspace, directions) = await CreateApprovedProfileAsync(scope, context);
        (await workspace.UpdateAsync(seed.TeamId!.Value, ProfileRequest([seed.StartupIndustryIds[0]]),
            seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();

        var reviewed = await ReviewAsync(directions, seed, "Approved");

        reviewed.IsSuccess.Should().BeTrue($"{reviewed.Error?.Code}: {reviewed.Error?.Message}");
        reviewed.Value.Status.Should().Be(nameof(ProjectDirectionStatus.Approved));
        reviewed.Value.ProposedStartupIndustries.Should().BeNull();
        reviewed.Value.StartupIndustries.Should().Equal(await IndustryNamesAsync(context, seed.StartupIndustryIds[0]));
        context.ChangeTracker.Clear();
        (await context.ProjectDirections.AsNoTracking().SingleAsync(item => item.TeamId == seed.TeamId))
            .ProposedStartupIndustries.Should().BeNull();
    }

    [Fact]
    public async Task ProfileIndustryChange_WhenRejected_KeepsTheApprovedIndustries()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (seed, workspace, directions) = await CreateApprovedProfileAsync(scope, context);
        (await workspace.UpdateAsync(seed.TeamId!.Value, ProfileRequest([seed.StartupIndustryIds[0]]),
            seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();

        var reviewed = await ReviewAsync(directions, seed, "Rejected");

        reviewed.IsSuccess.Should().BeTrue($"{reviewed.Error?.Code}: {reviewed.Error?.Message}");
        reviewed.Value.Status.Should().Be(nameof(ProjectDirectionStatus.Approved));
        reviewed.Value.IsProjectProfileChangeProposal.Should().BeFalse();
        reviewed.Value.ProposedStartupIndustries.Should().BeNull();
        reviewed.Value.StartupIndustries.Should().BeEquivalentTo(await IndustryNamesAsync(context, seed.StartupIndustryIds[0], seed.StartupIndustryIds[1]));
    }

    [Fact]
    public async Task ProfileIndustryChange_TogetherWithANameChange_IsApprovedAsOneRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (seed, workspace, directions) = await CreateApprovedProfileAsync(scope, context);
        var request = ProfileRequest([seed.StartupIndustryIds[1]], "Campus Circular Plus");

        (await workspace.UpdateAsync(seed.TeamId!.Value, request, seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();
        var reviewed = await ReviewAsync(directions, seed, "Approved");

        reviewed.IsSuccess.Should().BeTrue($"{reviewed.Error?.Code}: {reviewed.Error?.Message}");
        reviewed.Value.Title.Should().Be("Campus Circular Plus");
        reviewed.Value.StartupIndustries.Should().Equal(await IndustryNamesAsync(context, seed.StartupIndustryIds[1]));
    }

    [Fact]
    public async Task ProfileIndustryChange_WhenSelectionIsUnchanged_DoesNotOpenAReview()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (seed, workspace, directions) = await CreateApprovedProfileAsync(scope, context);

        var update = await workspace.UpdateAsync(seed.TeamId!.Value, ProfileRequest(seed.StartupIndustryIds[..2]),
            seed.ProposerUserId, SystemRoles.Student);

        update.IsSuccess.Should().BeTrue();
        var direction = (await directions.GetAsync(seed.TeamId.Value, seed.ProposerUserId, SystemRoles.Student)).Value;
        direction.Status.Should().Be(nameof(ProjectDirectionStatus.Approved));
        direction.ProposedStartupIndustries.Should().BeNull();
    }

    [Fact]
    public async Task ProfileIndustryChange_RejectsInvalidSelections()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (seed, workspace, _) = await CreateApprovedProfileAsync(scope, context);

        var inactive = await workspace.UpdateAsync(seed.TeamId!.Value, ProfileRequest([seed.StartupIndustryIds[2]]),
            seed.ProposerUserId, SystemRoles.Student);
        var empty = await workspace.UpdateAsync(seed.TeamId.Value, ProfileRequest([]),
            seed.ProposerUserId, SystemRoles.Student);
        var duplicate = await workspace.UpdateAsync(seed.TeamId.Value,
            ProfileRequest([seed.StartupIndustryIds[0], seed.StartupIndustryIds[0]]), seed.ProposerUserId, SystemRoles.Student);

        inactive.IsFailure.Should().BeTrue();
        empty.IsFailure.Should().BeTrue();
        duplicate.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ProfileIndustryChange_AfterRevisionRequest_TheTeamCanEditIndustriesAndResubmit()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (seed, workspace, directions) = await CreateApprovedProfileAsync(scope, context);
        (await workspace.UpdateAsync(seed.TeamId!.Value, ProfileRequest([seed.StartupIndustryIds[0]]),
            seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();
        var revisionRequested = await ReviewAsync(directions, seed, "NeedsRevision");
        revisionRequested.IsSuccess.Should().BeTrue();

        // The team now asks for the other industry instead.
        var revised = await directions.SaveAsync(seed.TeamId.Value, new SaveProjectDirectionRequest
        {
            Title = revisionRequested.Value.Title,
            Summary = revisionRequested.Value.Summary,
            StartupIndustryIds = [seed.StartupIndustryIds[1]],
            RowVersion = revisionRequested.Value.RowVersion
        }, seed.ProposerUserId, SystemRoles.Student);

        revised.IsSuccess.Should().BeTrue($"{revised.Error?.Code}: {revised.Error?.Message}");
        revised.Value.ProposedStartupIndustries.Should().Equal(await IndustryNamesAsync(context, seed.StartupIndustryIds[1]));
        var submitted = await directions.SubmitAsync(seed.TeamId.Value,
            new ProjectDirectionStateRequest { RowVersion = revised.Value.RowVersion }, seed.ProposerUserId, SystemRoles.Student);
        submitted.IsSuccess.Should().BeTrue($"{submitted.Error?.Code}: {submitted.Error?.Message}");
        submitted.Value.IsProjectProfileChangeProposal.Should().BeTrue();

        var approved = await ReviewAsync(directions, seed, "Approved");
        approved.IsSuccess.Should().BeTrue($"{approved.Error?.Code}: {approved.Error?.Message}");
        approved.Value.StartupIndustries.Should().Equal(await IndustryNamesAsync(context, seed.StartupIndustryIds[1]));
    }

    private async Task<(WorkflowSeed Seed, ProjectWorkspaceHandler Workspace, ProjectDirectionHandler Directions)> CreateApprovedProfileAsync(
        IServiceScope scope, AppDbContext context)
    {
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var workspace = new ProjectWorkspaceHandler(context, scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        var created = await workspace.CreateAsync(seed.TeamId!.Value, new CreateProjectWorkspaceRequest
        {
            ProjectName = ProfileName,
            Description = ProfileDescription,
            ZaloGroupUrl = ProfileZaloUrl,
            StartupIndustryIds = seed.StartupIndustryIds[..2]
        }, seed.ProposerUserId, SystemRoles.Student);
        created.IsSuccess.Should().BeTrue($"{created.Error?.Code}: {created.Error?.Message}");

        context.ChangeTracker.Clear();
        var directions = new ProjectDirectionHandler(context);
        var approved = await ReviewAsync(directions, seed, "Approved");
        approved.IsSuccess.Should().BeTrue($"{approved.Error?.Code}: {approved.Error?.Message}");
        context.ChangeTracker.Clear();
        return (seed, workspace, directions);
    }

    private static async Task<EHub.Shared.Results.Result<ProjectDirectionDto>> ReviewAsync(
        ProjectDirectionHandler directions, WorkflowSeed seed, string decision)
    {
        var current = await directions.GetAsync(seed.TeamId!.Value, seed.ProposerUserId, SystemRoles.Student);
        return await directions.ReviewAsync(seed.TeamId.Value, new ReviewProjectDirectionRequest
        {
            Decision = decision,
            Comment = decision == "Approved" ? null : "Please review this again.",
            RowVersion = current.Value.RowVersion
        }, seed.LecturerId, SystemRoles.Lecturer);
    }

    private static UpdateProjectWorkspaceRequest ProfileRequest(IReadOnlyCollection<Guid> industryIds, string name = ProfileName) => new()
    {
        ProjectName = name,
        Description = ProfileDescription,
        ZaloGroupUrl = ProfileZaloUrl,
        StartupIndustryIds = industryIds
    };

    private static async Task<string[]> IndustryNamesAsync(AppDbContext context, params Guid[] ids) =>
        await context.StartupIndustries.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => item.Name).ToArrayAsync();
}
