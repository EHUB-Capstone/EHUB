using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Application.Features.Workspaces.ReviewDesk;
using EHub.Contracts.Workspaces;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.IntegrationTests.Classes;

// The review inbox: which teams a reviewer may see, what is new since they last looked, and that it vanishes with AI summaries.
public sealed partial class TeamWorkflowIntegrationTests
{
    private static ReviewDeskHandler CreateReviewDesk(UploadFixture fixture, CheckpointSummarySettings? settings = null)
    {
        // One DbContext per request in production: nothing tracked by an earlier call may leak into this one.
        fixture.Context.ChangeTracker.Clear();
        return new ReviewDeskHandler(fixture.Context, fixture.Clock, settings ?? EnabledSummaries);
    }

    [Fact]
    public async Task ReviewDesk_ListsOnlyTheTeamsOfTheReviewer()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Lecturer, "desk-outsider");
        var teamId = fixture.Seed.TeamId!.Value;

        var lecturer = await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.LecturerId, SystemRoles.Lecturer);
        lecturer.Value.Inbox.Should().ContainSingle(item => item.TeamId == teamId && item.Reason == "NewVersion");
        (await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.AdminId, SystemRoles.Admin)).Value.Inbox
            .Should().Contain(item => item.TeamId == teamId);
        // The seeded mentor is assigned to TeamId only and sees nothing else of the class.
        (await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.MentorUserId, SystemRoles.Mentor)).Value.Inbox
            .Should().OnlyContain(item => item.TeamId == teamId);
        // A lecturer of another class has no teams here, so nothing is listed for them.
        (await CreateReviewDesk(fixture).GetDeskAsync(outsider.Id, SystemRoles.Lecturer)).Value.Inbox.Should().BeEmpty();
        (await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.ProposerUserId, SystemRoles.Student))
            .Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    [Fact]
    public async Task ReviewSeen_RemembersWhatEachReviewerHasOpenedAndANewUploadIsNewAgain()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        async Task<bool> IsNew(Guid userId, string role) =>
            (await CreateReviewDesk(fixture).GetDeskAsync(userId, role)).Value.Inbox.Any(item => item.TeamId == teamId && item.Reason == "NewVersion");

        (await CreateReviewDesk(fixture).MarkSeenAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();

        (await IsNew(fixture.Seed.LecturerId, SystemRoles.Lecturer)).Should().BeFalse();
        // The mentor has not opened it: what one person saw is not marked as seen for another.
        (await IsNew(fixture.Seed.MentorUserId, SystemRoles.Mentor)).Should().BeTrue();
        // Opening again changes nothing and does not add a second row.
        (await CreateReviewDesk(fixture).MarkSeenAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await fixture.Context.CheckpointReviewMarkers.CountAsync(item => item.UserId == fixture.Seed.LecturerId && item.TeamId == teamId)).Should().Be(1);

        await fixture.UploadStoredFileAsync("slides.docx", BuildDocx(CompetitorText));

        (await IsNew(fixture.Seed.LecturerId, SystemRoles.Lecturer)).Should().BeTrue();
        await CreateReviewDesk(fixture).MarkSeenAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        (await IsNew(fixture.Seed.LecturerId, SystemRoles.Lecturer)).Should().BeFalse();
        (await fixture.Context.CheckpointReviewMarkers.SingleAsync(item => item.UserId == fixture.Seed.LecturerId && item.TeamId == teamId))
            .SeenVersionNumber.Should().Be(2);

        // Students and reviewers of other teams cannot record a visit.
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Lecturer, "seen-outsider");
        (await CreateReviewDesk(fixture).MarkSeenAsync(teamId, 1, fixture.Seed.ProposerUserId, SystemRoles.Student))
            .Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await CreateReviewDesk(fixture).MarkSeenAsync(teamId, 1, outsider.Id, SystemRoles.Lecturer))
            .Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    [Fact]
    public async Task ReviewDesk_ShowsNewVersionsAndMissedDeadlinesToTheTeamsReviewers()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        var otherTeamId = fixture.Seed.OtherTeamId!.Value;

        var first = await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.LecturerId, SystemRoles.Lecturer);

        first.Value.Inbox.Should().ContainSingle(item => item.TeamId == teamId && item.Reason == "NewVersion" && item.VersionNumber == 1);

        await CreateReviewDesk(fixture).MarkSeenAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        (await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.LecturerId, SystemRoles.Lecturer))
            .Value.Inbox.Should().NotContain(item => item.TeamId == teamId, "the lecturer has opened the newest version");

        // The deadline passes while the other team has submitted nothing: that team leads the list.
        await fixture.ReopenCheckpointAsync(endsIn: TimeSpan.FromHours(1));
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(3);
        var later = await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.LecturerId, SystemRoles.Lecturer);
        later.Value.Inbox.First().Should().Match<ReviewDeskInboxItemDto>(item => item.TeamId == otherTeamId && item.Reason == "Overdue");

        // The mentor reviews TeamId only, so the other team's missed deadline is not theirs to see.
        var mentor = await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.MentorUserId, SystemRoles.Mentor);
        mentor.Value.Inbox.Should().NotContain(item => item.TeamId == otherTeamId);

        (await CreateReviewDesk(fixture).GetDeskAsync(fixture.Seed.ProposerUserId, SystemRoles.Student))
            .Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    [Fact]
    public async Task ReviewDesk_DoesNotExistWhenAiSummariesAreOff()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var off = CreateReviewDesk(fixture, new CheckpointSummarySettings { Enabled = false });
        var teamId = fixture.Seed.TeamId!.Value;

        off.Enabled.Should().BeFalse();
        CreateReviewDesk(fixture).Enabled.Should().BeTrue();
        (await off.GetDeskAsync(fixture.Seed.LecturerId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
        (await off.MarkSeenAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
        fixture.Context.CheckpointReviewMarkers.Where(item => item.UserId == fixture.Seed.LecturerId).Should().BeEmpty("nothing is recorded while the feature is off");
    }
}
