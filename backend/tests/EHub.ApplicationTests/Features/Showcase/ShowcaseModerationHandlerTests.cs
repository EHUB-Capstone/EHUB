using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseModerationHandlerTests
{
    private static readonly SubmitShowcaseRequest Acknowledged = new() { AcknowledgedPublishWarning = true };

    // ---- Submit ----

    [Theory]
    [InlineData(ShowcaseStatus.Draft)]
    [InlineData(ShowcaseStatus.Hidden)]
    public async Task Submit_MovesEditablePagesToPendingReview_AndNotifiesTheClassLecturer(ShowcaseStatus from)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, from);

        var result = await Handler(db).SubmitAsync(seed.ShowcaseId, Acknowledged, seed.OwnerUserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(ShowcaseStatus.PendingReview));
        var history = await db.ShowcaseReviewHistories.SingleAsync();
        (history.FromStatus, history.ToStatus, history.ActorId).Should().Be((from, ShowcaseStatus.PendingReview, seed.OwnerUserId));
        var data = OutboxData(db);
        data.GetProperty("recipientUserIds").EnumerateArray().Select(item => item.GetGuid()).Should().Equal(seed.LecturerUserId);
        data.GetProperty("notifyAdministrators").GetBoolean().Should().BeFalse();
        data.GetProperty("link").GetString().Should().Be("/showcase/moderation");
    }

    [Fact]
    public async Task Submit_FallsBackToAdministrators_WhenTheClassHasNoLecturer()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, withLecturer: false);

        var result = await Handler(db).SubmitAsync(seed.ShowcaseId, Acknowledged, seed.OwnerUserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        OutboxData(db).GetProperty("notifyAdministrators").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Submit_RequiresTheOwnerToAcknowledgeThePublishWarning()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db);

        var result = await Handler(db).SubmitAsync(seed.ShowcaseId, new SubmitShowcaseRequest(), seed.OwnerUserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        (await db.ShowcaseProjects.SingleAsync()).Status.Should().Be(ShowcaseStatus.Draft);
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Submit_RequiresTaglineProblemAndSolution()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, complete: false);

        var result = await Handler(db).SubmitAsync(seed.ShowcaseId, Acknowledged, seed.OwnerUserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
    }

    [Theory]
    [InlineData(ShowcaseStatus.PendingReview)]
    [InlineData(ShowcaseStatus.Published)]
    public async Task Submit_IsRejectedForPagesAlreadyPendingOrLive(ShowcaseStatus from)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, from);

        var result = await Handler(db).SubmitAsync(seed.ShowcaseId, Acknowledged, seed.OwnerUserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
    }

    [Fact]
    public async Task Submit_ByAnotherStudentIsNotFound_AndByOtherRolesIsDenied()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db);
        var handler = Handler(db);

        var stranger = await handler.SubmitAsync(seed.ShowcaseId, Acknowledged, Guid.NewGuid(), SystemRoles.Student);
        var lecturer = await handler.SubmitAsync(seed.ShowcaseId, Acknowledged, seed.LecturerUserId, SystemRoles.Lecturer);

        stranger.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        lecturer.Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await db.ShowcaseProjects.SingleAsync()).Status.Should().Be(ShowcaseStatus.Draft);
    }

    // ---- Withdraw ----

    [Theory]
    [InlineData(ShowcaseStatus.Published)]
    [InlineData(ShowcaseStatus.PendingReview)]
    public async Task Withdraw_HidesALiveOrPendingPage(ShowcaseStatus from)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, from);

        var result = await Handler(db).WithdrawAsync(seed.ShowcaseId, seed.OwnerUserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(ShowcaseStatus.Hidden));
        result.Value.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task Withdraw_IsRejectedForDraftsAndForOtherTeams()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db);
        var handler = Handler(db);

        var draft = await handler.WithdrawAsync(seed.ShowcaseId, seed.OwnerUserId, SystemRoles.Student);
        var stranger = await handler.WithdrawAsync(seed.ShowcaseId, Guid.NewGuid(), SystemRoles.Student);

        draft.Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
        stranger.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
    }

    // ---- Decisions ----

    [Fact]
    public async Task Approve_PublishesThePage_AndNotifiesTheTeam()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);

        var result = await Handler(db).DecideAsync(
            seed.ShowcaseId, ShowcaseModerationAction.Approve, null, seed.LecturerUserId, SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(ShowcaseStatus.Published));
        result.Value.PublishedAt.Should().NotBeNull();
        var history = await db.ShowcaseReviewHistories.SingleAsync();
        (history.ToStatus, history.ActorId).Should().Be((ShowcaseStatus.Published, seed.LecturerUserId));
        OutboxData(db).GetProperty("recipientUserIds").EnumerateArray().Select(item => item.GetGuid())
            .Should().Equal(seed.OwnerUserId);
    }

    [Fact]
    public async Task Approve_KeepsTheOriginalPublishDate_WhenAPageIsApprovedAgain()
    {
        await using var db = ShowcaseTestData.NewContext();
        var original = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview, publishedAt: original);

        var result = await Handler(db).DecideAsync(
            seed.ShowcaseId, ShowcaseModerationAction.Approve, null, seed.LecturerUserId, SystemRoles.Lecturer);

        result.Value.PublishedAt.Should().Be(original);
    }

    [Theory]
    [InlineData(ShowcaseModerationAction.Reject, ShowcaseStatus.PendingReview, ShowcaseStatus.Draft)]
    [InlineData(ShowcaseModerationAction.Hide, ShowcaseStatus.Published, ShowcaseStatus.Hidden)]
    public async Task NegativeDecisions_RequireANote_AndStoreItForTheTeam(
        ShowcaseModerationAction action, ShowcaseStatus from, ShowcaseStatus to)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, from);
        var handler = Handler(db);

        var withoutNote = await handler.DecideAsync(seed.ShowcaseId, action, "   ", seed.LecturerUserId, SystemRoles.Lecturer);
        var withNote = await handler.DecideAsync(seed.ShowcaseId, action, "  Remove the personal phone number.  ", seed.LecturerUserId, SystemRoles.Lecturer);

        withoutNote.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        withNote.IsSuccess.Should().BeTrue();
        withNote.Value.Status.Should().Be(to.ToString());
        withNote.Value.ReviewNote.Should().Be("Remove the personal phone number.");
        OutboxData(db).GetProperty("body").GetString().Should().Contain("Remove the personal phone number.");
    }

    [Fact]
    public async Task Decide_RejectsAnOverlongNote()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);

        var result = await Handler(db).DecideAsync(
            seed.ShowcaseId, ShowcaseModerationAction.Reject,
            new string('x', ShowcaseModerationHandler.MaximumNoteLength + 1), seed.LecturerUserId, SystemRoles.Lecturer);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
    }

    [Theory]
    [InlineData(ShowcaseModerationAction.Approve, ShowcaseStatus.Draft)]
    [InlineData(ShowcaseModerationAction.Approve, ShowcaseStatus.Published)]
    [InlineData(ShowcaseModerationAction.Reject, ShowcaseStatus.Published)]
    [InlineData(ShowcaseModerationAction.Hide, ShowcaseStatus.PendingReview)]
    [InlineData(ShowcaseModerationAction.Unhide, ShowcaseStatus.Published)]
    public async Task Decide_RejectsTransitionsThatSkipTheWorkflow(ShowcaseModerationAction action, ShowcaseStatus current)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, current);

        var result = await Handler(db).DecideAsync(seed.ShowcaseId, action, "note", seed.LecturerUserId, SystemRoles.Lecturer);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
        (await db.ShowcaseProjects.SingleAsync()).Status.Should().Be(current);
        db.ShowcaseReviewHistories.Should().BeEmpty();
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Unhide_RestoresAPreviouslyPublishedPage_ButNotOneThatWasNeverPublished()
    {
        await using var db = ShowcaseTestData.NewContext();
        var published = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Hidden, publishedAt: DateTime.UtcNow.AddDays(-3));
        var never = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Hidden);
        var handler = Handler(db);

        var restored = await handler.DecideAsync(published.ShowcaseId, ShowcaseModerationAction.Unhide, null, published.LecturerUserId, SystemRoles.Lecturer);
        var blocked = await handler.DecideAsync(never.ShowcaseId, ShowcaseModerationAction.Unhide, null, never.LecturerUserId, SystemRoles.Lecturer);

        restored.Value.Status.Should().Be(nameof(ShowcaseStatus.Published));
        blocked.Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
    }

    [Fact]
    public async Task Decide_IsLimitedToTheLecturersOwnClasses_ButAdminsReviewEverything()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);
        var otherLecturer = await ShowcaseTestData.AddUserAsync(db, "Other lecturer");
        var handler = Handler(db);

        var outsider = await handler.DecideAsync(seed.ShowcaseId, ShowcaseModerationAction.Approve, null, otherLecturer.Id, SystemRoles.Lecturer);
        var admin = await handler.DecideAsync(seed.ShowcaseId, ShowcaseModerationAction.Approve, null, Guid.NewGuid(), SystemRoles.Admin);

        outsider.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        admin.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    public async Task Decide_IsDeniedForStudentsAndMentors(string role)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);

        var result = await Handler(db).DecideAsync(seed.ShowcaseId, ShowcaseModerationAction.Approve, null, seed.OwnerUserId, role);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await db.ShowcaseProjects.SingleAsync()).Status.Should().Be(ShowcaseStatus.PendingReview);
    }

    // ---- Queue ----

    [Fact]
    public async Task List_ShowsLecturersOnlyTheirPendingPages_AndAdminsAll()
    {
        await using var db = ShowcaseTestData.NewContext();
        var mine = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);
        await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);
        await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Draft, lecturerUserId: mine.LecturerUserId);
        var handler = Handler(db);

        var lecturer = await handler.ListAsync(null, 1, 20, mine.LecturerUserId, SystemRoles.Lecturer);
        var admin = await handler.ListAsync("pendingreview", 1, 20, Guid.NewGuid(), SystemRoles.Admin);

        lecturer.Value.Items.Should().ContainSingle().Which.Id.Should().Be(mine.ShowcaseId);
        lecturer.Value.Items.Single().TeamName.Should().Be("Garden Team");
        lecturer.Value.Total.Should().Be(1);
        admin.Value.Total.Should().Be(2);
    }

    [Fact]
    public async Task List_PagesResults_AndValidatesTheStatusFilter()
    {
        await using var db = ShowcaseTestData.NewContext();
        var first = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);
        await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview, lecturerUserId: first.LecturerUserId);
        await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview, lecturerUserId: first.LecturerUserId);
        var handler = Handler(db);

        var page = await handler.ListAsync("PendingReview", 2, 2, first.LecturerUserId, SystemRoles.Lecturer);
        var invalid = await handler.ListAsync("Nonsense", 1, 20, first.LecturerUserId, SystemRoles.Lecturer);
        var student = await handler.ListAsync(null, 1, 20, first.OwnerUserId, SystemRoles.Student);

        page.Value.Items.Should().ContainSingle();
        page.Value.Total.Should().Be(3);
        invalid.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        student.Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
    }

    private static ShowcaseModerationHandler Handler(AppDbContext db)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTime.UtcNow);
        return new ShowcaseModerationHandler(db, clock);
    }

    /// <summary>The "data" object of the single queued outbox message.</summary>
    private static JsonElement OutboxData(AppDbContext db)
    {
        var message = db.OutboxMessages.AsNoTracking().Single();
        message.Type.Should().Be("Showcase.Notification.v1");
        return JsonDocument.Parse(message.PayloadJson).RootElement.GetProperty("data").Clone();
    }
}
