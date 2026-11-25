using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Application.Features.Showcase.Reports;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseReportScopeTests
{
    [Theory]
    [InlineData(SystemRoles.Admin, false)]
    [InlineData(SystemRoles.Admin, true)]
    [InlineData(SystemRoles.Lecturer, false)]
    [InlineData(SystemRoles.Lecturer, true)]
    public async Task SoftDeletedShowcase_HidesPageAndCommentReports_AndRejectsResolution(string role, bool commentTarget)
    {
        await using var db = ShowcaseTestData.NewContext();
        var target = await SeedTargetAsync(db, commentTarget);
        var handler = Handler(db);
        (await db.ShowcaseProjects.SingleAsync(x => x.Id == target.ShowcaseId)).IsDeleted = true;
        await db.SaveChangesAsync();
        db.ClearChanges();

        var queue = await handler.ListAsync("Open", 1, 20, target.LecturerId, role);
        queue.IsSuccess.Should().BeTrue();
        queue.Value.TotalItems.Should().Be(0);
        queue.Value.Items.Should().BeEmpty();
        foreach (var decision in new[] { "Dismissed", "ActionTaken" })
        {
            var result = await handler.ResolveAsync(target.ReportId, new() { Status = decision }, target.LecturerId, role);
            result.Error.Code.Should().Be(ErrorCodes.ShowcaseReportNotFound);
        }

        (await db.ShowcaseReports.SingleAsync(x => x.Id == target.ReportId)).Status.Should().Be(ShowcaseReportStatus.Open);
        (await db.OutboxMessages.CountAsync()).Should().Be(target.NotificationCount);
        if (commentTarget)
            (await db.ShowcaseComments.IgnoreQueryFilters().SingleAsync(x => x.Id == target.CommentId)).IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(SystemRoles.Admin, ShowcaseReportStatus.Dismissed)]
    [InlineData(SystemRoles.Admin, ShowcaseReportStatus.ActionTaken)]
    [InlineData(SystemRoles.Lecturer, ShowcaseReportStatus.Dismissed)]
    [InlineData(SystemRoles.Lecturer, ShowcaseReportStatus.ActionTaken)]
    public async Task SoftDeletedShowcase_HistoryCannotBeViewedOrDeleted(string role, ShowcaseReportStatus status)
    {
        await using var db = ShowcaseTestData.NewContext();
        var target = await SeedTargetAsync(db, false);
        (await db.ShowcaseReports.SingleAsync(x => x.Id == target.ReportId)).Status = status;
        (await db.ShowcaseProjects.SingleAsync(x => x.Id == target.ShowcaseId)).IsDeleted = true;
        await db.SaveChangesAsync();
        db.ClearChanges();
        var handler = Handler(db);

        (await handler.ListAsync(status.ToString(), 1, 20, target.LecturerId, role)).Value.Items.Should().BeEmpty();
        (await handler.DeleteHistoryAsync(target.ReportId, target.LecturerId, role)).Error.Code.Should().Be(ErrorCodes.ShowcaseReportNotFound);
        (await handler.DeleteAllHistoryAsync(status.ToString(), target.LecturerId, role)).IsSuccess.Should().BeTrue();
        (await db.ShowcaseReports.SingleAsync(x => x.Id == target.ReportId)).Status.Should().Be(status);
    }

    [Theory]
    [InlineData(SystemRoles.Admin, "Dismissed")]
    [InlineData(SystemRoles.Admin, "ActionTaken")]
    [InlineData(SystemRoles.Lecturer, "Dismissed")]
    [InlineData(SystemRoles.Lecturer, "ActionTaken")]
    public async Task MixedSources_OnlyHumanReportsAreCountedResolvedAndPruned(string role, string decision)
    {
        await using var db = ShowcaseTestData.NewContext();
        var target = await SeedTargetAsync(db, true);
        var system = new ShowcaseReport
        {
            ShowcaseProjectId = target.ShowcaseId, CommentId = target.CommentId, Source = ShowcaseReportSource.System,
            Reason = ShowcaseReportReason.Harassment, ContentFingerprint = new string('a', 64), DetectionKey = new string('b', 64)
        };
        db.ShowcaseReports.Add(system);
        await db.SaveChangesAsync();
        var handler = Handler(db);

        var item = (await handler.ListAsync("Open", 1, 20, target.LecturerId, role)).Value.Items.Should().ContainSingle().Which;
        item.Id.Should().Be(target.ReportId);
        item.Sources.Should().BeEquivalentTo(new[] { "User" });
        item.ReportCount.Should().Be(1);
        item.SystemReportCount.Should().Be(0);
        item.SystemNote.Should().BeNull();
        (await handler.ResolveAsync(system.Id, new() { Status = "ActionTaken" }, target.LecturerId, role))
            .Error.Code.Should().Be(ErrorCodes.ShowcaseReportNotFound);
        (await handler.ResolveAsync(item.Id, new() { Status = decision }, target.LecturerId, role)).IsSuccess.Should().BeTrue();
        system.Status.Should().Be(ShowcaseReportStatus.Open);
        (await db.ShowcaseComments.IgnoreQueryFilters().SingleAsync(x => x.Id == target.CommentId)).IsDeleted.Should().Be(decision == "ActionTaken");
        (await db.ShowcaseProjects.SingleAsync(x => x.Id == target.ShowcaseId)).CommentCount.Should().Be(decision == "ActionTaken" ? 0 : 1);
        (await db.OutboxMessages.CountAsync()).Should().Be(target.NotificationCount + (decision == "ActionTaken" ? 1 : 0));
        system.Status = Enum.Parse<ShowcaseReportStatus>(decision);
        await db.SaveChangesAsync();
        (await handler.DeleteHistoryAsync(system.Id, target.LecturerId, role)).Error.Code.Should().Be(ErrorCodes.ShowcaseReportNotFound);
        (await handler.DeleteHistoryAsync(item.Id, target.LecturerId, role)).IsSuccess.Should().BeTrue();
        (await db.ShowcaseReports.SingleAsync()).Id.Should().Be(system.Id);
        (await handler.DeleteAllHistoryAsync(decision, target.LecturerId, role)).IsSuccess.Should().BeTrue();
        (await db.ShowcaseReports.SingleAsync()).Id.Should().Be(system.Id);
    }

    private static async Task<Target> SeedTargetAsync(AppDbContext db, bool commentTarget)
    {
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        Guid? commentId = null;
        if (commentTarget)
        {
            var comment = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = seed.OwnerUserId, Content = "Constructive comment" };
            db.ShowcaseComments.Add(comment);
            commentId = comment.Id;
            (await db.ShowcaseProjects.SingleAsync(x => x.Id == seed.ShowcaseId)).CommentCount = 1;
            await db.SaveChangesAsync();
        }
        var receipt = await Handler(db).CreateAsync(seed.ShowcaseId, new() { Reason = "Harassment", CommentId = commentId }, reporter.Id);
        receipt.IsSuccess.Should().BeTrue();
        return new Target(seed.ShowcaseId, receipt.Value.Id, commentId, seed.LecturerUserId, await db.OutboxMessages.CountAsync());
    }

    private static ShowcaseReportHandler Handler(AppDbContext db)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc));
        return new ShowcaseReportHandler(db, clock, new ShowcaseModerationHandler(db, clock),
            new CreateShowcaseReportRequestValidator(), new ResolveShowcaseReportRequestValidator());
    }

    private sealed record Target(Guid ShowcaseId, Guid ReportId, Guid? CommentId, Guid LecturerId, int NotificationCount);
}
