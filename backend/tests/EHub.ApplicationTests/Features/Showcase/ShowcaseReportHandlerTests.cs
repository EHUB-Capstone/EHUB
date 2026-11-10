using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Manage;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Application.Features.Showcase.Reports;
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

public sealed class ShowcaseReportHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CreateShowcaseReportRequest PageReport = new() { Reason = "Spam", Note = "Private report note" };

    [Fact]
    public async Task Create_RejectsDuplicatesEvenAfterDismissal_AndAllowsAnotherReporter()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        var another = await ShowcaseTestData.AddUserAsync(db, "Another");
        var handler = Handler(db);
        var created = await handler.CreateAsync(seed.ShowcaseId, PageReport, reporter.Id);
        created.IsSuccess.Should().BeTrue();
        created.Value.CreatedAt.Should().Be(Now);
        JsonSerializer.Serialize(created.Value).Should().NotContain("Reporter");
        (await handler.CreateAsync(seed.ShowcaseId, PageReport, reporter.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseReportAlreadyExists);
        (await handler.ResolveAsync(created.Value.Id, new() { Status = "Dismissed" }, seed.LecturerUserId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await handler.CreateAsync(seed.ShowcaseId, PageReport, reporter.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseReportAlreadyExists);
        (await handler.CreateAsync(seed.ShowcaseId, PageReport, another.Id)).IsSuccess.Should().BeTrue();
        (await db.ShowcaseReports.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(ShowcaseStatus.Draft)]
    [InlineData(ShowcaseStatus.PendingReview)]
    [InlineData(ShowcaseStatus.Hidden)]
    public async Task Create_RejectsNonPublishedPages(ShowcaseStatus state)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, state);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        (await Handler(db).CreateAsync(seed.ShowcaseId, PageReport, reporter.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        db.ShowcaseReports.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_RequiresAuthentication_AndRejectsTeamReportingItsOwnPage()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var owner = await ShowcaseTestData.AddUserAsync(db, "Owner");
        (await db.Students.SingleAsync(item => item.UserId == seed.OwnerUserId)).UserId = owner.Id;
        await db.SaveChangesAsync();
        var handler = Handler(db);
        (await handler.CreateAsync(seed.ShowcaseId, PageReport, Guid.Empty)).Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
        (await handler.CreateAsync(seed.ShowcaseId, PageReport, owner.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
    }

    [Fact]
    public async Task Comments_AreSeparateTargets_MustBelongToThePage_AndCannotBeSelfReported()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var foreign = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        var author = await ShowcaseTestData.AddUserAsync(db, "Author");
        var first = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = author.Id, Content = "First" };
        var second = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = author.Id, Content = "Second", ParentCommentId = first.Id };
        db.ShowcaseComments.AddRange(first, second);
        await db.SaveChangesAsync();
        var handler = Handler(db);
        var request = new CreateShowcaseReportRequest { Reason = "Harassment", CommentId = first.Id };
        (await handler.CreateAsync(foreign.ShowcaseId, request, reporter.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseCommentNotFound);
        (await handler.CreateAsync(seed.ShowcaseId, request, author.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await handler.CreateAsync(seed.ShowcaseId, request, reporter.Id)).IsSuccess.Should().BeTrue();
        (await handler.CreateAsync(seed.ShowcaseId, request, reporter.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseReportAlreadyExists);
        (await handler.CreateAsync(seed.ShowcaseId, new() { Reason = "Spam", CommentId = second.Id }, reporter.Id)).IsSuccess.Should().BeTrue();
        (await handler.CreateAsync(seed.ShowcaseId, PageReport, reporter.Id)).IsSuccess.Should().BeTrue();
        first.IsDeleted = true;
        await db.SaveChangesAsync();
        (await handler.CreateAsync(seed.ShowcaseId, request, author.Id)).Error.Code.Should().Be(ErrorCodes.ShowcaseCommentNotFound);
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    public async Task QueueAndResolve_RejectNonStaff(string role)
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db);
        (await handler.ListAsync(null, 1, 20, Guid.NewGuid(), role)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await handler.ResolveAsync(Guid.NewGuid(), new() { Status = "Dismissed" }, Guid.NewGuid(), role)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
    }

    [Fact]
    public async Task QueueAndResolve_RestrictLecturersToAssignedClasses_AdminSeesAll()
    {
        await using var db = ShowcaseTestData.NewContext();
        var own = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var foreign = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        var handler = Handler(db);
        var first = await handler.CreateAsync(own.ShowcaseId, PageReport, reporter.Id);
        var second = await handler.CreateAsync(foreign.ShowcaseId, PageReport, reporter.Id);
        var queue = await handler.ListAsync(null, 1, 20, own.LecturerUserId, SystemRoles.Lecturer);
        queue.Value.Items.Should().ContainSingle().Which.Id.Should().Be(first.Value.Id);
        (await handler.ResolveAsync(second.Value.Id, new() { Status = "Dismissed" }, own.LecturerUserId, SystemRoles.Lecturer))
            .Error.Code.Should().Be(ErrorCodes.ShowcaseReportNotFound);
        var admin = await handler.ListAsync("Open", 1, 100, Guid.NewGuid(), SystemRoles.Admin);
        admin.Value.TotalItems.Should().Be(2);
        admin.Value.PageSize.Should().Be(50);
        // ClassLecturers assignments also grant scope when the primary lecturer differs.
        db.ClassLecturers.Add(new ClassLecturer { ClassId = foreign.ClassId, LecturerId = own.LecturerUserId });
        await db.SaveChangesAsync();
        (await handler.ListAsync(null, 1, 20, own.LecturerUserId, SystemRoles.Lecturer)).Value.TotalItems.Should().Be(2);
    }

    [Theory]
    [InlineData("Dismissed")]
    [InlineData("ActionTaken")]
    public async Task Resolve_RecordsUtcDecision_AndOnlyActionTakenHidesAndNotifies(string status)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var page = await db.ShowcaseProjects.SingleAsync();
        page.IsFeatured = true;
        page.FeaturedAt = Now;
        await db.SaveChangesAsync();
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Secret reporter");
        var handler = Handler(db);
        var created = await handler.CreateAsync(seed.ShowcaseId, PageReport, reporter.Id);
        var resolved = await handler.ResolveAsync(created.Value.Id,
            new() { Status = status, ResolutionNote = "Private moderator note" }, seed.LecturerUserId, SystemRoles.Lecturer);
        resolved.IsSuccess.Should().BeTrue();
        resolved.Value.Status.Should().Be(status);
        resolved.Value.ResolvedAt.Should().Be(Now);
        resolved.Value.ResolvedByUserId.Should().Be(seed.LecturerUserId);
        page.Status.Should().Be(status == "ActionTaken" ? ShowcaseStatus.Hidden : ShowcaseStatus.Published);
        if (status == "ActionTaken")
        {
            page.IsFeatured.Should().BeFalse();
            page.FeaturedAt.Should().BeNull();
            var payload = (await db.OutboxMessages.SingleAsync()).PayloadJson;
            payload.Should().NotContain("Private").And.NotContain(reporter.Id.ToString()).And.NotContain(reporter.FullName);
            page.ReviewNote.Should().NotContain("Private");
            db.ShowcaseReviewHistories.Select(item => item.Note).Should().OnlyContain(note => !note!.Contains("Private"));
            // Verify the same DTO the team uses cannot expose report notes.
            var mine = await new ShowcaseManageHandler(db, null!, Clock(), new SaveShowcaseRequestValidator())
                .GetMineAsync(seed.OwnerUserId, SystemRoles.Student);
            JsonSerializer.Serialize(mine.Value).Should().NotContain("Private").And.NotContain(reporter.FullName);
        }
        else
        {
            db.OutboxMessages.Should().BeEmpty();
            db.ShowcaseReviewHistories.Should().BeEmpty();
        }
        (await handler.ResolveAsync(created.Value.Id, new() { Status = "Dismissed" }, seed.LecturerUserId, SystemRoles.Lecturer))
            .Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
    }

    [Fact]
    public async Task ActionTaken_OnAlreadyHiddenPage_ClosesAnotherReportWithoutDuplicateNotification()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        var another = await ShowcaseTestData.AddUserAsync(db, "Another");
        var handler = Handler(db);
        var first = await handler.CreateAsync(seed.ShowcaseId, PageReport, reporter.Id);
        var second = await handler.CreateAsync(seed.ShowcaseId, PageReport, another.Id);
        var decision = new ResolveShowcaseReportRequest { Status = "ActionTaken" };
        (await handler.ResolveAsync(first.Value.Id, decision, seed.LecturerUserId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await handler.ResolveAsync(second.Value.Id, decision, seed.LecturerUserId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        db.OutboxMessages.Should().ContainSingle();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("")]
    [InlineData("Open")]
    public async Task Resolve_RejectsInvalidTransitions(string status)
    {
        await using var db = ShowcaseTestData.NewContext();
        (await Handler(db).ResolveAsync(Guid.NewGuid(), new() { Status = status }, Guid.NewGuid(), SystemRoles.Admin))
            .Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("Unknown")]
    [InlineData("")]
    public async Task Create_RejectsInvalidReasons(string reason)
    {
        await using var db = ShowcaseTestData.NewContext();
        var user = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        (await Handler(db).CreateAsync(Guid.NewGuid(), new() { Reason = reason }, user.Id))
            .Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    private static ShowcaseReportHandler Handler(AppDbContext db) => new(
        db, Clock(), new ShowcaseModerationHandler(db, Clock()),
        new CreateShowcaseReportRequestValidator(), new ResolveShowcaseReportRequestValidator());
}
