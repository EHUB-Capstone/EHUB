using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Infrastructure.Summarization;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;

namespace EHub.IntegrationTests.Classes;

// Comparing a replaced file with its previous version: reviewers only, nothing at all while AI summaries are off.
public sealed partial class TeamWorkflowIntegrationTests
{
    private static CheckpointVersionChangesHandler CreateVersionChanges(UploadFixture fixture, CheckpointSummarySettings? settings = null)
    {
        fixture.Context.ChangeTracker.Clear();
        var used = settings ?? EnabledSummaries;
        return new CheckpointVersionChangesHandler(fixture.Context,
            new CheckpointFileText(fixture.Context, fixture.Storage, new InMemoryCheckpointStorage(), new SubmissionTextExtractor(), used), used);
    }

    [Fact]
    public async Task VersionChanges_ComparesAReplacedFileWithItsPreviousVersionForReviewersOnly()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText + "\n" + CompetitorText));
        // The team uploads plan.docx again: the survey grew and a new paragraph was added. A second file is new to the reviewer.
        var second = SurveyText.Replace("150", "320") + "\n" + CompetitorText + "\nMô hình doanh thu dựa trên gói đăng ký hằng tháng cho cửa hàng nhỏ.";
        var replaced = await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(second));
        await fixture.UploadStoredFileAsync("slides.docx", BuildDocx(CompetitorText));

        var result = await CreateVersionChanges(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        var plan = result.Value.Files.Single(item => item.FileName == "plan.docx");
        plan.Status.Should().Be("Compared");
        plan.FileId.Should().Be(replaced.Id);
        plan.ToVersion.Should().BeGreaterThan(plan.FromVersion);
        plan.ChangedCount.Should().Be(1);
        plan.AddedCount.Should().Be(1);
        plan.Items.Should().Contain(item => item.Kind == "Changed" && item.FiguresChanged && item.Before!.Contains("150") && item.Text.Contains("320"));
        plan.Items.Should().Contain(item => item.Kind == "Added" && item.Text.Contains("gói đăng ký"));
        plan.UnchangedPercent.Should().BeInRange(1, 99);
        result.Value.Files.Single(item => item.FileName == "slides.docx").Status.Should().Be("NewFile");

        // The mentor assigned to the team may look; students, outsiders and unknown teams are refused alike.
        (await CreateVersionChanges(fixture).GetAsync(teamId, 1, fixture.Seed.MentorUserId, SystemRoles.Mentor)).IsSuccess.Should().BeTrue();
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Lecturer, "changes-outsider");
        foreach (var (userId, role) in new[] { (fixture.Seed.ProposerUserId, SystemRoles.Student), (outsider.Id, SystemRoles.Lecturer) })
            (await CreateVersionChanges(fixture).GetAsync(teamId, 1, userId, role)).Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await CreateVersionChanges(fixture).GetAsync(Guid.NewGuid(), 1, fixture.Seed.LecturerId, SystemRoles.Lecturer))
            .Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    [Fact]
    public async Task VersionChanges_ReportsAnIdenticalReupload_AndDoesNotExistWhenAiSummariesAreOff()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));

        var same = await CreateVersionChanges(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        same.Value.Files.Should().ContainSingle().Which.Status.Should().Be("Unchanged");

        var off = await CreateVersionChanges(fixture, new CheckpointSummarySettings { Enabled = false })
            .GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        off.Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
    }
}
