using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Data.Common;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Application.Features.Showcase.Reports;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace EHub.IntegrationTests.Showcase;

[Collection("Sequential")]
public sealed class ShowcaseReportIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task Reports_EnforceScope_HideWithOutbox_AndKeepReporterAndPrivateNotesFromTeam()
    {
        await using var scenario = await SeedAsync();
        using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Spam", Note = "Confidential reporter details" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipt = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!;
        using var foreign = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{receipt.Id}/resolve",
            scenario.Reporter, SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var foreignQueue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports", scenario.Reporter, SystemRoles.Lecturer);
        (await foreignQueue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Should().BeEmpty();
        using var queue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open&pageSize=100", scenario.Lecturer, SystemRoles.Lecturer);
        var page = (await queue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!;
        page.Items.Should().ContainSingle().Which.Id.Should().Be(receipt.Id);
        page.PageSize.Should().Be(50);
        using var resolved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{receipt.Id}/resolve",
            scenario.Lecturer, SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "ActionTaken", ResolutionNote = "Confidential review note" });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(item => item.Id == receipt.Id);
        report.Status.Should().Be(ShowcaseReportStatus.ActionTaken);
        report.ResolvedByUserId.Should().Be(scenario.Lecturer.Id);
        report.ResolvedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        var hidden = await scenario.Db.ShowcaseProjects.AsNoTracking().SingleAsync(item => item.Id == scenario.Page.Id);
        hidden.Status.Should().Be(ShowcaseStatus.Hidden);
        hidden.IsFeatured.Should().BeFalse();
        hidden.FeaturedAt.Should().BeNull();
        var notifications = await scenario.Db.OutboxMessages.Where(item => item.AggregateId == hidden.Id).ToListAsync();
        notifications.Should().HaveCount(2);
        notifications.Should().Contain(item => item.PayloadJson.Contains("New showcase report"));
        notifications.Should().OnlyContain(item => item.PayloadJson.Contains("Confidential") == false
            && item.PayloadJson.Contains(scenario.Reporter.Id.ToString()) == false
            && item.PayloadJson.Contains(scenario.Reporter.FullName) == false);
        using var mine = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/mine", scenario.Owner, SystemRoles.Student);
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        (await mine.Content.ReadAsStringAsync()).Should().NotContain("Confidential").And.NotContain(scenario.Reporter.FullName);
        using var repeated = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{receipt.Id}/resolve",
            scenario.Lecturer, SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "Dismissed" });
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ConcurrentDuplicateReports_CreateExactlyOne_PerTarget()
    {
        await using var scenario = await SeedAsync();
        async Task AssertSingleAsync(Guid? commentId)
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
                    new CreateShowcaseReportRequest { Reason = "Harassment", CommentId = commentId })));
            try
            {
                responses.Select(response => response.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            }
            finally
            {
                foreach (var response in responses) response.Dispose();
            }
        }
        await AssertSingleAsync(null);
        var comment = new ShowcaseComment { ShowcaseProjectId = scenario.Page.Id, AuthorUserId = scenario.Owner.Id, Content = "A comment" };
        scenario.Db.ShowcaseComments.Add(comment);
        await scenario.Db.SaveChangesAsync();
        await AssertSingleAsync(comment.Id);
        (await scenario.Db.ShowcaseReports.CountAsync(item => item.ShowcaseProjectId == scenario.Page.Id)).Should().Be(2);
        // Both filtered unique indexes remain effective after resolution.
        await scenario.Db.ShowcaseReports.Where(item => item.ShowcaseProjectId == scenario.Page.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ShowcaseReportStatus.Dismissed));
        foreach (var commentId in new Guid?[] { null, comment.Id })
        {
            scenario.Db.ShowcaseReports.Add(new ShowcaseReport
            {
                ShowcaseProjectId = scenario.Page.Id, CommentId = commentId, ReporterUserId = scenario.Reporter.Id, CreatedAt = DateTime.UtcNow
            });
            await FluentActions.Awaiting(() => scenario.Db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
            scenario.Db.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task ConcurrentResolutions_CommitOnlyOneDecisionAndItsSideEffects()
    {
        await using var scenario = await SeedAsync();
        using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Mentor,
            new CreateShowcaseReportRequest { Reason = "Other" });
        var id = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
        var responses = await Task.WhenAll(new[] { "ActionTaken", "Dismissed" }.Select(status =>
            scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{id}/resolve",
                scenario.Lecturer, status == "Dismissed" ? SystemRoles.Admin : SystemRoles.Lecturer,
                new ResolveShowcaseReportRequest { Status = status })));
        try
        {
            responses.Select(response => response.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
        var report = await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(item => item.Id == id);
        var page = await scenario.Db.ShowcaseProjects.AsNoTracking().SingleAsync(item => item.Id == scenario.Page.Id);
        var acted = report.Status == ShowcaseReportStatus.ActionTaken;
        page.Status.Should().Be(acted ? ShowcaseStatus.Hidden : ShowcaseStatus.Published);
        (await scenario.Db.OutboxMessages.CountAsync(item => item.AggregateId == page.Id)).Should().Be(acted ? 2 : 1);
        (await scenario.Db.ShowcaseReviewHistories.CountAsync(item => item.ShowcaseProjectId == page.Id && item.ToStatus == ShowcaseStatus.Hidden))
            .Should().Be(acted ? 1 : 0);
    }

    [Theory]
    [InlineData("ActionTaken", "Dismissed")]
    [InlineData("Dismissed", "ActionTaken")]
    public async Task ResolveAfterAnotherReviewerWinsBetweenReads_HasNoSecondDecisionOrSideEffects(string losingStatus, string winningStatus)
    {
        await using var scenario = await SeedAsync();
        using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Other" });
        var id = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
        var interceptor = new ResolveBetweenReadsInterceptor(async () =>
        {
            // Commit the winning request after the other handler read Open, but before its open-report query executes.
            using var winner = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{id}/resolve",
                scenario.Lecturer, SystemRoles.Admin, new ResolveShowcaseReportRequest { Status = winningStatus });
            winner.StatusCode.Should().Be(HttpStatusCode.OK);
        });
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(factory.TestConnectionString, options => options.UseVector()).AddInterceptors(interceptor).Options);
        var clock = scenario.Scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var handler = new ShowcaseReportHandler(db, clock, new ShowcaseModerationHandler(db, clock),
            new CreateShowcaseReportRequestValidator(), new ResolveShowcaseReportRequestValidator());

        var loser = await handler.ResolveAsync(id, new ResolveShowcaseReportRequest { Status = losingStatus },
            scenario.Lecturer.Id, SystemRoles.Lecturer);

        interceptor.Triggered.Should().BeTrue();
        loser.IsFailure.Should().BeTrue();
        loser.Error.Code.Should().Be(ErrorCodes.ShowcaseConcurrencyConflict);
        var report = await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(item => item.Id == id);
        report.Status.ToString().Should().Be(winningStatus);
        var acted = winningStatus == "ActionTaken";
        var page = await scenario.Db.ShowcaseProjects.AsNoTracking().SingleAsync(item => item.Id == scenario.Page.Id);
        page.Status.Should().Be(acted ? ShowcaseStatus.Hidden : ShowcaseStatus.Published);
        (await scenario.Db.OutboxMessages.CountAsync(item => item.AggregateId == page.Id)).Should().Be(acted ? 2 : 1);
        (await scenario.Db.ShowcaseReviewHistories.CountAsync(item => item.ShowcaseProjectId == page.Id && item.ToStatus == ShowcaseStatus.Hidden))
            .Should().Be(acted ? 1 : 0);
    }

    [Fact]
    public async Task CommentReports_CheckOwnershipAndPage_AndActionTakenHidesOnlyTheReportedComment()
    {
        await using var scenario = await SeedAsync();
        var comment = new ShowcaseComment { ShowcaseProjectId = scenario.Page.Id, AuthorUserId = scenario.Reporter.Id, Content = "Reported comment" };
        scenario.Db.ShowcaseComments.Add(comment);
        await scenario.Db.SaveChangesAsync();
        var body = new CreateShowcaseReportRequest { CommentId = comment.Id, Reason = "InappropriateContent" };
        using var self = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student, body);
        self.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var missingComment = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Owner, SystemRoles.Student,
            new CreateShowcaseReportRequest { CommentId = Guid.NewGuid(), Reason = "Other" });
        missingComment.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Owner, SystemRoles.Student, body);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipt = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!;
        receipt.CommentId.Should().Be(comment.Id);
        using var resolved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{receipt.Id}/resolve",
            scenario.Lecturer, SystemRoles.Admin, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.Db.ShowcaseProjects.AsNoTracking().SingleAsync(item => item.Id == scenario.Page.Id))
            .Status.Should().Be(ShowcaseStatus.Published);
        (await scenario.Db.ShowcaseComments.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id == comment.Id))
            .IsDeleted.Should().BeTrue();
        var notifications = await scenario.Db.OutboxMessages.Where(item => item.AggregateId == scenario.Page.Id).ToListAsync();
        notifications.Should().HaveCount(2);
        notifications.Should().Contain(item => item.PayloadJson.Contains("Bình luận của bạn đã bị ẩn vì vi phạm quy tắc cộng đồng."));
        notifications.Should().OnlyContain(item => item.PayloadJson.Contains(scenario.Owner.FullName) == false);
    }

    [Fact]
    public async Task FailedSave_DoesNotCloseReportHidePageOrQueueNotification()
    {
        await using var scenario = await SeedAsync();
        using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Other" });
        var id = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
        var options = scenario.Scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        await using var failingDb = new AppDbContext(options);
        // Keep the losing reviewer's original xmin, then commit a decision in another context.
        await failingDb.ShowcaseReports.SingleAsync(item => item.Id == id);
        await failingDb.ShowcaseProjects.SingleAsync(item => item.Id == scenario.Page.Id);
        await scenario.Db.ShowcaseReports.Where(item => item.Id == id)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, ShowcaseReportStatus.Dismissed)
                .SetProperty(item => item.ResolvedAt, DateTime.UtcNow)
                .SetProperty(item => item.ResolvedByUserId, scenario.Lecturer.Id));
        var clock = scenario.Scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var handler = new ShowcaseReportHandler(failingDb, clock, new ShowcaseModerationHandler(failingDb, clock),
            new CreateShowcaseReportRequestValidator(), new ResolveShowcaseReportRequestValidator());
        var result = await handler.ResolveAsync(id, new() { Status = "ActionTaken" }, scenario.Lecturer.Id, SystemRoles.Lecturer);
        result.IsFailure.Should().BeTrue();
        (await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(item => item.Id == id)).Status.Should().Be(ShowcaseReportStatus.Dismissed);
        (await scenario.Db.ShowcaseProjects.AsNoTracking().SingleAsync(item => item.Id == scenario.Page.Id)).Status.Should().Be(ShowcaseStatus.Published);
        (await scenario.Db.OutboxMessages.CountAsync(item => item.AggregateId == scenario.Page.Id)).Should().Be(1,
            "the creation notification remains, but the failed action must not queue another notification");
        (await scenario.Db.ShowcaseReviewHistories.AnyAsync(item => item.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Creation_RejectsOwnAndUnpublishedPages_AndRateLimitsPerUser()
    {
        await using var scenario = await SeedAsync();
        using var own = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Owner, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Spam" });
        own.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var reportableComments = Enumerable.Range(0, 6).Select(index => new ShowcaseComment
        {
            ShowcaseProjectId = scenario.Page.Id,
            AuthorUserId = scenario.Owner.Id,
            Content = $"Rate-limit comment {index}"
        }).ToArray();
        scenario.Db.ShowcaseComments.AddRange(reportableComments);
        await scenario.Db.SaveChangesAsync();

        for (var i = 0; i < 5; i++)
        {
            using var allowed = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.RateReporter, SystemRoles.Student,
                new CreateShowcaseReportRequest { Reason = "Spam", CommentId = reportableComments[i].Id });
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        using var limited = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.RateReporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Spam", CommentId = reportableComments[5].Id });
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        await scenario.Db.ShowcaseProjects.Where(item => item.Id == scenario.Page.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ShowcaseStatus.Hidden));
        using var hidden = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Spam" });
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var independent = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseReportRequest { Reason = "Spam" });
        independent.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResolvedHistory_CanBeDeletedIndividuallyOrByCurrentStatus()
    {
        await using var scenario = await SeedAsync();

        async Task<Guid> CreateAndDismissAsync()
        {
            using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
                new CreateShowcaseReportRequest { Reason = "Other" });
            created.StatusCode.Should().Be(HttpStatusCode.OK);
            var id = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
            using var dismissed = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{id}/resolve",
                scenario.Lecturer, SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "Dismissed" });
            dismissed.StatusCode.Should().Be(HttpStatusCode.OK);
            return id;
        }

        var first = await CreateAndDismissAsync();
        using (var deleted = await scenario.SendAsync(HttpMethod.Delete, $"/api/showcase/reports/{first}", scenario.Lecturer, SystemRoles.Lecturer))
            deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.Db.ShowcaseReports.CountAsync()).Should().Be(0);

        await CreateAndDismissAsync();
        using (var deletedAll = await scenario.SendAsync(HttpMethod.Delete, "/api/showcase/reports/history?status=Dismissed", scenario.Lecturer, SystemRoles.Lecturer))
            deletedAll.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.Db.ShowcaseReports.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.Lecturer)]
    public async Task InternalSystemFlags_AreExcludedFromStaffQueue_AndCannotBeResolved(string role)
    {
        await using var scenario = await SeedAsync();
        scenario.Page.Highlights = "đồ ngu";
        await scenario.Db.SaveChangesAsync();
        async Task ScanAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IShowcaseAutoReportScanner>().ScanPageAsync(scenario.Page.Id);
        }
        await Task.WhenAll(ScanAsync(), ScanAsync());
        var report = await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(x => x.ShowcaseProjectId == scenario.Page.Id);
        report.Source.Should().Be(ShowcaseReportSource.System);
        report.ReporterUserId.Should().BeNull();
        (await scenario.Db.ShowcaseModerationScans.CountAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().Be(1);
        using var openQueue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports", scenario.Lecturer, role);
        (await openQueue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items
            .Should().NotContain(item => item.Id == report.Id);
        (await scenario.Db.OutboxMessages.AnyAsync(x => x.AggregateId == scenario.Page.Id)).Should().BeFalse();
        using var otherClass = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{report.Id}/resolve", scenario.Reporter,
            SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        otherClass.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var resolved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{report.Id}/resolve", scenario.Lecturer,
            role, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        resolved.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var queue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports", scenario.Lecturer, SystemRoles.Lecturer);
        (await queue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Should().NotContain(x => x.Id == report.Id);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id)).Status.Should().Be(ShowcaseStatus.Published);
    }

    [Fact]
    public async Task InternalSystemFlags_CannotBeActionedAfterContentChangesOrRescan()
    {
        await using var scenario = await SeedAsync();
        scenario.Page.Status = ShowcaseStatus.PendingReview;
        scenario.Page.Highlights = "đồ ngu";
        await scenario.Db.SaveChangesAsync();
        var scanner = scenario.Scope.ServiceProvider.GetRequiredService<IShowcaseAutoReportScanner>();
        await scanner.ScanPageAsync(scenario.Page.Id);
        var report = await scenario.Db.ShowcaseReports.SingleAsync(x => x.ShowcaseProjectId == scenario.Page.Id);
        scenario.Project.Description = "Changed after the scan";
        await scenario.Db.SaveChangesAsync();
        using var stale = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{report.Id}/resolve", scenario.Lecturer,
            SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        stale.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await scanner.ScanPageAsync(scenario.Page.Id);
        var current = await scenario.Db.ShowcaseReports.SingleAsync(x => x.ShowcaseProjectId == scenario.Page.Id && x.Status == ShowcaseReportStatus.Open);
        using var resolved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{current.Id}/resolve", scenario.Lecturer,
            SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        resolved.StatusCode.Should().Be(HttpStatusCode.NotFound);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id)).Status.Should().Be(ShowcaseStatus.PendingReview);
    }

    [Fact]
    public async Task RuleEndpoints_RequireAuthenticationAndAdmin_AndRejectMissingRowVersion()
    {
        await using var scenario = await SeedAsync();
        using var anonymous = await scenario.Client.GetAsync("/api/showcase/moderation-rules");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor, SystemRoles.Lecturer })
        {
            using var denied = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/moderation-rules", scenario.Reporter, role);
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var deniedWrite = await scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Reporter, role,
                new SaveShowcaseModerationRuleRequest { Phrase = "test phrase", Description = "Test rule" });
            deniedWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        using var created = await scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Reporter, SystemRoles.Admin,
            new SaveShowcaseModerationRuleRequest { Phrase = "test phrase", Description = "Test rule", Category = "Threat" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var rule = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!;
        rule.Category.Should().Be("Threat");
        try
        {
            using var conflict = await scenario.SendAsync(HttpMethod.Put, $"/api/showcase/moderation-rules/{rule.Id}", scenario.Reporter, SystemRoles.Admin,
                new SaveShowcaseModerationRuleRequest { Phrase = "test phrase", Description = "Changed" });
            conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
            using var updated = await scenario.SendAsync(HttpMethod.Put, $"/api/showcase/moderation-rules/{rule.Id}", scenario.Reporter, SystemRoles.Admin,
                new SaveShowcaseModerationRuleRequest { Phrase = "test phrase", Description = "Changed", IsEnabled = false, RowVersion = rule.RowVersion });
            updated.StatusCode.Should().Be(HttpStatusCode.OK);
            (await updated.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!.Category.Should().Be("Threat");
            using var invalidCategory = await scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Reporter, SystemRoles.Admin,
                new SaveShowcaseModerationRuleRequest { Phrase = "invalid category phrase", Description = "Test rule", Category = "Unknown" });
            invalidCategory.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally { await scenario.Db.ShowcaseModerationRules.Where(x => x.Id == rule.Id).ExecuteDeleteAsync(); }
    }

    [Fact]
    public async Task DuplicateComments_AreRejected_WithoutChangingCounters()
    {
        await using var scenario = await SeedAsync();
        var path = $"/api/showcase/{scenario.Page.Id}/comments";
        using var first = await scenario.SendAsync(HttpMethod.Post, path, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseCommentRequest { Content = "This is a repeated substantive comment" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        using var repeated = await scenario.SendAsync(HttpMethod.Post, path, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseCommentRequest { Content = "THIS IS A REPEATED SUBSTANTIVE COMMENT" });
        repeated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id)).CommentCount.Should().Be(1);
        (await scenario.Db.ShowcaseComments.CountAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData("cdmm")]
    [InlineData("clm")]
    [InlineData("clgt")]
    [InlineData("óc cặc")]
    [InlineData("óc loz")]
    [InlineData("oc lon")]
    [InlineData("fuck")]
    [InlineData("chó đẻ")]
    [InlineData("dume")]
    [InlineData("duma")]
    [InlineData("đỉ mẹ")]
    [InlineData("địt mẹ")]
    [InlineData("tao sẽ giết mày")]
    [InlineData("đồ súc sinh")]
    [InlineData("đồ lừa đảo")]
    [InlineData("lũ da đen")]
    [InlineData("phim sex")]
    [InlineData("ngu")]
    [InlineData("ngu ghê")]
    [InlineData("bạn ngu")]
    [InlineData("NGU!")]
    [InlineData("Góp ý:\nngu ghê")]
    [InlineData("bạn\tngu\tghê")]
    [InlineData("óc bò")]
    [InlineData("thật khốn nạn")]
    [InlineData("con cặc")]
    [InlineData("cái loz")]
    [InlineData("cai lồn")]
    [InlineData("cặc")]
    [InlineData("loz")]
    [InlineData("lồn")]
    [InlineData("CON CẶC!")]
    [InlineData("cái (loz)")]
    [InlineData("Bạn nói cái lồn gì")]
    [InlineData("lợn")]
    [InlineData("heo")]
    [InlineData("đồ lợn")]
    [InlineData("HEO!")]
    public async Task ProhibitedComment_IsRejectedWithoutSavingReportingNotifyingOrCounting(string content)
    {
        await using var scenario = await SeedAsync();
        using var posted = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Reporter,
            SystemRoles.Student, new CreateShowcaseCommentRequest { Content = content });
        posted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await posted.Content.ReadAsStringAsync()).Should().Contain(ErrorCodes.ShowcaseLanguageBlocked);
        scenario.Db.ChangeTracker.Clear();
        var page = await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id);
        page.Status.Should().Be(ShowcaseStatus.Published); page.CommentCount.Should().Be(0);
        (await scenario.Db.ShowcaseComments.AnyAsync(x => x.ShowcaseProjectId == page.Id)).Should().BeFalse();
        (await scenario.Db.ShowcaseReports.AnyAsync(x => x.ShowcaseProjectId == page.Id)).Should().BeFalse();
        (await scenario.Db.OutboxMessages.AnyAsync(x => x.AggregateId == page.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ProhibitedReply_DoesNotChangeTheExistingThreadOrCounters()
    {
        await using var scenario = await SeedAsync();
        var root = new ShowcaseComment { ShowcaseProjectId = scenario.Page.Id, AuthorUserId = scenario.Reporter.Id, Content = "How does your product work?" };
        scenario.Db.ShowcaseComments.Add(root);
        scenario.Page.CommentCount = 1;
        await scenario.Db.SaveChangesAsync();
        using var response = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Owner,
            SystemRoles.Student, new CreateShowcaseCommentRequest { Content = "chủng tộc hạ đẳng", ParentCommentId = root.Id });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ErrorCodes.ShowcaseLanguageBlocked);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id)).CommentCount.Should().Be(1);
        (await scenario.Db.ShowcaseComments.CountAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().Be(1);
        (await scenario.Db.ShowcaseReports.AnyAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();
        (await scenario.Db.OutboxMessages.AnyAsync(x => x.AggregateId == scenario.Page.Id)).Should().BeFalse();
    }

    [Theory]
    [InlineData("Ứng dụng giáo dục giới tính")]
    [InlineData("Dự án tìm hiểu và phòng chống phân biệt chủng tộc")]
    [InlineData("Ứng dụng giúp ngủ ngon")]
    [InlineData("Nguyễn nghiên cứu nguồn nước và nguyên liệu")]
    [InlineData("Các bạn cùng tham gia")]
    [InlineData("cac ban cung tham gia")]
    [InlineData("Ứng dụng cho dự án lớn")]
    [InlineData("Tái chế vỏ lon")]
    [InlineData("cái lon")]
    [InlineData("cai lon")]
    [InlineData("lớn")]
    [InlineData("lộn")]
    [InlineData("nghèo")]
    public async Task NeutralEducationalComment_IsAccepted(string content)
    {
        await using var scenario = await SeedAsync();
        using var response = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Reporter,
            SystemRoles.Student, new CreateShowcaseCommentRequest { Content = content });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id)).CommentCount.Should().Be(1);
        (await scenario.Db.ShowcaseComments.SingleAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Content.Should().Be(content);
        (await scenario.Db.ShowcaseReports.AnyAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();
    }

    [Theory]
    [InlineData("con cặc", "PROFANITY")]
    [InlineData("ngu ghê", "INSULT")]
    [InlineData("tao giết mày", "THREAT")]
    [InlineData("đồ lừa đảo", "DEFAMATION")]
    [InlineData("lũ da đen", "RACISM")]
    [InlineData("phim sex", "ADULTCONTENT")]
    [InlineData("ngu con cặc", "PROFANITY_INSULT")]
    [InlineData("cdmm clm clgt óc cặc óc loz oc lon fuck dume duma phim sex", "PROFANITY_ADULTCONTENT")]
    [InlineData("lợn heo", "INSULT")]
    public async Task BlockedComment_ReturnsOnlyTheMatchedCategories(string content, string suffix)
    {
        await using var scenario = await SeedAsync();
        using var response = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Reporter,
            SystemRoles.Student, new CreateShowcaseCommentRequest { Content = content });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be(ErrorCodes.ShowcaseLanguageBlocked + "_" + suffix);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseComments.AnyAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();
        (await scenario.Db.ShowcaseReports.AnyAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();
        (await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id)).CommentCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Profanity", "Harassment")]
    [InlineData("Insult", "Harassment")]
    [InlineData("Threat", "Harassment")]
    [InlineData("Defamation", "Harassment")]
    [InlineData("Racism", "Harassment")]
    [InlineData("AdultContent", "InappropriateContent")]
    [InlineData("Spam", "Spam")]
    public async Task RuleCategory_RoundTripsThroughPostgresAndDto(string category, string reason)
    {
        await using var scenario = await SeedAsync();
        using var response = await scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Reporter, SystemRoles.Admin,
            new SaveShowcaseModerationRuleRequest { Phrase = "unique rule " + Guid.NewGuid().ToString("N"), Description = "Category round trip", Category = category });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rule = (await response.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!;
        try
        {
            rule.Category.Should().Be(category);
            rule.Reason.Should().Be(reason);
            var stored = await scenario.Db.ShowcaseModerationRules.AsNoTracking().SingleAsync(x => x.Id == rule.Id);
            stored.Category.ToString().Should().Be(category);
            using var listed = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/moderation-rules", scenario.Reporter, SystemRoles.Admin);
            var rules = (await listed.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto[]>>())!.Data!;
            rules.Single(x => x.Id == rule.Id).Category.Should().Be(category);
        }
        finally { await scenario.Db.ShowcaseModerationRules.Where(x => x.Id == rule.Id).ExecuteDeleteAsync(); }
    }

    private static async Task<Guid> SeedProposalCommentAsync(Scenario scenario)
    {
        var comment = new ShowcaseComment { ShowcaseProjectId = scenario.Page.Id, AuthorUserId = scenario.Owner.Id, Content = "Bạn là đồ đần độn!" };
        scenario.Db.ShowcaseComments.Add(comment); scenario.Page.CommentCount = 1; await scenario.Db.SaveChangesAsync();
        using var posted = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { CommentId = comment.Id, Reason = "Harassment", Note = "This note is not the comment" });
        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await posted.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
    }

    [Fact]
    public async Task Proposal_WaitsForAdminApproval_AndSurvivesReportHistoryDeletion()
    {
        await using var scenario = await SeedAsync();
        var reportId = await SeedProposalCommentAsync(scenario);
        using var queueResponse = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open", scenario.Lecturer, SystemRoles.Lecturer);
        var queued = (await queueResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Single(x => x.Id == reportId);
        queued.CommentContent.Should().Be("Bạn là đồ đần độn!"); queued.CommentContent.Should().NotBe(queued.Note);
        var count = await scenario.Db.ShowcaseModerationRules.CountAsync();
        using var created = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseRuleProposalRequest { Phrase = "ĐẦN ĐỘN", Category = "Insult" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposal = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        proposal.Status.Should().Be("Pending");
        (await scenario.Db.ShowcaseModerationRules.CountAsync()).Should().Be(count);
        (await scenario.Db.ShowcaseProjects.AsNoTracking().SingleAsync(x => x.Id == scenario.Page.Id)).CommentCount.Should().Be(1);
        using var teacherReview = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/moderation-rule-proposals/{proposal.Id}/review", scenario.Lecturer, SystemRoles.Lecturer,
            new ReviewShowcaseRuleProposalRequest { Status = "Approved", RowVersion = proposal.RowVersion });
        teacherReview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var stale = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/moderation-rule-proposals/{proposal.Id}/review", scenario.Lecturer, SystemRoles.Admin,
            new ReviewShowcaseRuleProposalRequest { Status = "Approved", RowVersion = proposal.RowVersion + 1 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var hidden = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/resolve", scenario.Lecturer, SystemRoles.Lecturer,
            new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        hidden.StatusCode.Should().Be(HttpStatusCode.OK);
        using var deletedHistory = await scenario.SendAsync(HttpMethod.Delete, $"/api/showcase/reports/{reportId}", scenario.Lecturer, SystemRoles.Lecturer);
        deletedHistory.StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseModerationRuleProposals.SingleAsync(x => x.Id == proposal.Id)).SourceReportId.Should().BeNull();
        using var refreshedQueue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/moderation-rule-proposals", scenario.Lecturer, SystemRoles.Admin);
        var refreshed = (await refreshedQueue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseRuleProposalDto>>>())!.Data!.Items.Single(x => x.Id == proposal.Id);
        using var approved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/moderation-rule-proposals/{proposal.Id}/review", scenario.Lecturer, SystemRoles.Admin,
            new ReviewShowcaseRuleProposalRequest { Status = "Approved", RowVersion = refreshed.RowVersion });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await approved.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        result.Status.Should().Be("Approved");
        var rule = await scenario.Db.ShowcaseModerationRules.AsNoTracking().SingleAsync(x => x.Id == result.CreatedRuleId);
        rule.Status.Should().Be(ShowcaseModerationRuleStatus.Active); rule.Category.Should().Be(ShowcaseViolationCategory.Insult);
        using var blocked = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseCommentRequest { Content = "đần độn" });
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain(ErrorCodes.ShowcaseLanguageInsult);
    }

    [Fact]
    public async Task Proposal_ValidatesAuthenticationScopeAndLiteralText()
    {
        await using var scenario = await SeedAsync(); var reportId = await SeedProposalCommentAsync(scenario);
        var path = $"/api/showcase/reports/{reportId}/rule-proposals";
        using var anonymous = await scenario.Client.PostAsJsonAsync(path, new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var student = await scenario.SendAsync(HttpMethod.Post, path, scenario.Reporter, SystemRoles.Student, new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        student.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var outside = await scenario.SendAsync(HttpMethod.Post, path, scenario.Reporter, SystemRoles.Lecturer, new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        outside.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var invalidPhrase = await scenario.SendAsync(HttpMethod.Post, path, scenario.Lecturer, SystemRoles.Lecturer, new CreateShowcaseRuleProposalRequest { Phrase = "dan don", Category = "Insult" });
        invalidPhrase.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalidPhrase.Content.ReadAsStringAsync()).Should().Contain(ErrorCodes.ShowcaseRuleProposalInvalidPhrase);
        using var teacherQueue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/moderation-rule-proposals", scenario.Lecturer, SystemRoles.Lecturer);
        teacherQueue.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.Db.ShowcaseModerationRuleProposals.AnyAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Proposal_UsesOnlyHumanReports_WhenSystemFlagsAlsoExist()
    {
        await using var scenario = await SeedAsync();
        var reportId = await SeedProposalCommentAsync(scenario);
        var userReport = await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(x => x.Id == reportId);
        var systemReport = new ShowcaseReport
        {
            ShowcaseProjectId = scenario.Page.Id, CommentId = userReport.CommentId,
            Source = ShowcaseReportSource.System, Reason = ShowcaseReportReason.Harassment,
            DetectionKey = Guid.NewGuid().ToString("N"), ContentFingerprint = "proposal-mixed-report-test", CreatedAt = DateTime.UtcNow
        };
        scenario.Db.ShowcaseReports.Add(systemReport); await scenario.Db.SaveChangesAsync();
        using var queue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open", scenario.Lecturer, SystemRoles.Lecturer);
        var item = (await queue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Single(x => x.CommentId == userReport.CommentId);
        item.Id.Should().Be(reportId);
        item.Sources.Should().Contain("User");
        item.Sources.Should().NotContain("System");
        using var invalidSource = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{systemReport.Id}/rule-proposals", scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        invalidSource.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var response = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{item.Id}/rule-proposals", scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposal = (await response.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        proposal.SourceReportId.Should().Be(reportId);
        proposal.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ConcurrentProposals_CountOnce_AndConcurrentReviews_CreateAtMostOneRule()
    {
        await using var scenario = await SeedAsync(); var reportId = await SeedProposalCommentAsync(scenario);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" })));
        foreach (var response in results) response.Dispose();
        results.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        var proposal = await scenario.Db.ShowcaseModerationRuleProposals.AsNoTracking().SingleAsync(x => x.ShowcaseProjectId == scenario.Page.Id);
        var reviews = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => scenario.SendAsync(HttpMethod.Post, $"/api/showcase/moderation-rule-proposals/{proposal.Id}/review", scenario.Lecturer, SystemRoles.Admin,
            new ReviewShowcaseRuleProposalRequest { Status = "Approved", RowVersion = proposal.Version })));
        foreach (var response in reviews) response.Dispose();
        reviews.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
        reviews.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        (await scenario.Db.ShowcaseModerationRules.CountAsync(x => x.Phrase == "đần độn")).Should().Be(1);
    }

    [Fact]
    public async Task AdminConfirmation_ActivatesImmediately_AndNextCommentIsBlocked()
    {
        await using var scenario = await SeedAsync(); var reportId = await SeedProposalCommentAsync(scenario);
        using var response = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, SystemRoles.Admin,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        result.Status.Should().Be("Approved"); result.CreatedRuleId.Should().NotBeNull(); result.ReviewedAtUtc.Should().NotBeNull();
        (await scenario.Db.ShowcaseModerationRuleProposals.AsNoTracking().SingleAsync(x => x.Id == result.Id)).ReviewedByUserId.Should().Be(scenario.Lecturer.Id);
        (await scenario.Db.ShowcaseModerationRuleProposals.CountAsync(x => x.ShowcaseProjectId == scenario.Page.Id && x.Status == ShowcaseRuleProposalStatus.Pending)).Should().Be(0);
        using var blocked = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseCommentRequest { Content = "đần độn" });
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain(ErrorCodes.ShowcaseLanguageInsult);
    }

    [Fact]
    public async Task AdminConfirmation_ApprovesExistingTeacherProposal_WithoutCreatingADuplicate()
    {
        await using var scenario = await SeedAsync(); var reportId = await SeedProposalCommentAsync(scenario);
        using var proposed = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        proposed.StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = (await proposed.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        pending.Status.Should().Be("Pending");
        using var approved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Reporter, SystemRoles.Admin,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Defamation" });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await approved.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        result.Id.Should().Be(pending.Id); result.Status.Should().Be("Approved"); result.Category.Should().Be("Defamation");
        var audit = await scenario.Db.ShowcaseModerationRuleProposals.AsNoTracking().SingleAsync(x => x.Id == result.Id);
        audit.ProposedByUserId.Should().Be(scenario.Lecturer.Id); audit.ReviewedByUserId.Should().Be(scenario.Reporter.Id);
        (await scenario.Db.ShowcaseModerationRules.CountAsync(x => x.Phrase == "đần độn")).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAdminConfirmations_CreateOneEnabledRuleAndOneApprovedAudit()
    {
        await using var scenario = await SeedAsync(); var reportId = await SeedProposalCommentAsync(scenario);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => scenario.SendAsync(HttpMethod.Post,
            $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, SystemRoles.Admin,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" })));
        try
        {
            var diagnostic = string.Join("; ", await Task.WhenAll(results.Select(async response => $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}")));
            results.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
            results.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1, diagnostic);
            (await scenario.Db.ShowcaseModerationRuleProposals.CountAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().Be(1);
            (await scenario.Db.ShowcaseModerationRules.CountAsync(x => x.Phrase == "đần độn" && x.Status == ShowcaseModerationRuleStatus.Active)).Should().Be(1);
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    [Fact]
    public async Task Catalog_IsAuthenticated_AndSpecificReportCategoryRoundTrips()
    {
        await using var scenario = await SeedAsync();
        using var anonymous = await scenario.Client.GetAsync("/api/showcase/violation-categories");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var catalogResponse = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/violation-categories", scenario.Reporter, SystemRoles.Student);
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalog = (await catalogResponse.Content.ReadFromJsonAsync<ApiResponse<ShowcaseViolationCategoryDto[]>>())!.Data!;
        catalog.Select(x => x.Code).Should().BeEquivalentTo(Enum.GetNames<ShowcaseViolationCategory>());
        catalog.Select(x => x.SortOrder).Should().BeInAscendingOrder();
        using var invalid = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Harassment", Category = "Spam" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Harassment", Category = "Threat" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipt = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!;
        (await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(x => x.Id == receipt.Id)).Category.Should().Be(ShowcaseViolationCategory.Threat);
        using var queueResponse = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports", scenario.Lecturer, SystemRoles.Lecturer);
        var row = (await queueResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Single(x => x.Id == receipt.Id);
        row.Category.Should().Be("Threat");
    }

    [Fact]
    public async Task CategoryMigration_BackfillsLegacyReports_EnforcesForeignKeys_AndRollsBackWithoutDeletingReports()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var schema = "category_upgrade_" + Guid.NewGuid().ToString("N");
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection)) await create.ExecuteNonQueryAsync();
        try
        {
            await using (var selectSchema = new NpgsqlCommand($"SET search_path TO \"{schema}\"", connection)) await selectSchema.ExecuteNonQueryAsync();
            async Task Execute(string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
            async Task<object?> Scalar(string sql) { await using var command = new NpgsqlCommand(sql, connection); return await command.ExecuteScalarAsync(); }
            // Minimal legacy fixtures isolate the actual migration's Up/Down scripts from the shared API-test schema.
            await Execute("""
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
                CREATE TABLE showcase_reports (id integer PRIMARY KEY, reason varchar(30) NOT NULL);
                CREATE TABLE showcase_moderation_rules (id integer PRIMARY KEY, category varchar(32) NOT NULL);
                CREATE TABLE showcase_moderation_rule_proposals (id integer PRIMARY KEY, category varchar(32) NOT NULL);
                INSERT INTO showcase_reports VALUES (1,'Spam'),(2,'Harassment'),(3,'InappropriateContent'),(4,'CopyrightViolation'),(5,'Other');
                INSERT INTO showcase_moderation_rules VALUES (1,'Profanity');
                INSERT INTO showcase_moderation_rule_proposals VALUES (1,'Threat');
                """);
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261124194224_AddShowcaseRuleProposals";
            const string current = "20261124210923_AddShowcaseViolationCategoryCatalog";
            await Execute(migrator.GenerateScript(previous, current));
            (await Scalar("SELECT count(*) FROM showcase_violation_categories")).Should().Be(7L);
            (await Scalar("SELECT string_agg(COALESCE(category,'unclassified'),',' ORDER BY id) FROM showcase_reports"))
                .Should().Be("Spam,Insult,AdultContent,unclassified,unclassified");
            var violation = await Assert.ThrowsAsync<PostgresException>(() => Execute("DELETE FROM showcase_violation_categories WHERE code='Insult'"));
            violation.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            await Execute(migrator.GenerateScript(current, previous));
            (await Scalar("SELECT count(*) FROM showcase_reports")).Should().Be(5L);
            (await Scalar("SELECT to_regclass('showcase_violation_categories') IS NULL")).Should().Be(true);
        }
        catch
        {
            await using var rollback = new NpgsqlCommand("ROLLBACK", connection);
            await rollback.ExecuteNonQueryAsync();
            throw;
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"RESET search_path; DROP SCHEMA \"{schema}\" CASCADE", connection);
            await cleanup.ExecuteNonQueryAsync();
            await db.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task ConcurrentAdminHandlers_ReturnBusinessConflictWithoutThrowing()
    {
        await using var scenario = await SeedAsync(); var reportId = await SeedProposalCommentAsync(scenario);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var request = new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" };
        var results = await Task.WhenAll(
            firstScope.ServiceProvider.GetRequiredService<ShowcaseRuleProposalHandler>().CreateAsync(reportId, request, scenario.Lecturer.Id, SystemRoles.Admin),
            secondScope.ServiceProvider.GetRequiredService<ShowcaseRuleProposalHandler>().CreateAsync(reportId, request, scenario.Lecturer.Id, SystemRoles.Admin));
        results.Count(x => x.IsSuccess).Should().Be(1);
        results.Where(x => x.IsFailure).Should().OnlyContain(x => x.Error.Code == ErrorCodes.ShowcaseConcurrencyConflict || x.Error.Code == ErrorCodes.ShowcaseRuleProposalAlreadyExists);
    }

    [Fact]
    public async Task ConcurrentAdminConfirmations_ForDifferentPhrases_DoNotReturnServerErrors()
    {
        await using var scenario = await SeedAsync(); var reportId = await SeedProposalCommentAsync(scenario);
        var results = await Task.WhenAll(new[] { "đần độn", "đần" }.Select(phrase => scenario.SendAsync(HttpMethod.Post,
            $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, SystemRoles.Admin,
            new CreateShowcaseRuleProposalRequest { Phrase = phrase, Category = "Insult" })));
        try
        {
            results.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK || x.StatusCode == HttpStatusCode.Conflict);
            results.Count(x => x.StatusCode == HttpStatusCode.OK).Should().BeGreaterThan(0);
            (await scenario.Db.ShowcaseModerationRuleProposals.CountAsync(x => x.ShowcaseProjectId == scenario.Page.Id))
                .Should().Be(results.Count(x => x.StatusCode == HttpStatusCode.OK));
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    [Theory]
    [InlineData(SystemRoles.Admin, false)]
    [InlineData(SystemRoles.Admin, true)]
    [InlineData(SystemRoles.Lecturer, false)]
    [InlineData(SystemRoles.Lecturer, true)]
    public async Task SoftDeletedShowcase_ExcludesReports_AndReturns404ForReviewActions(string role, bool commentTarget)
    {
        await using var scenario = await SeedAsync();
        Guid reportId;
        if (commentTarget)
            reportId = await SeedProposalCommentAsync(scenario);
        else
        {
            using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
                new CreateShowcaseReportRequest { Reason = "Spam" });
            created.StatusCode.Should().Be(HttpStatusCode.OK);
            reportId = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
        }
        var notificationCount = await scenario.Db.OutboxMessages.CountAsync(x => x.AggregateId == scenario.Page.Id);
        await scenario.Db.ShowcaseProjects.Where(x => x.Id == scenario.Page.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsDeleted, true));
        try
        {
            using var queue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open", scenario.Lecturer, role);
            queue.StatusCode.Should().Be(HttpStatusCode.OK);
            (await queue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items
                .Should().NotContain(x => x.ShowcaseProjectId == scenario.Page.Id);
            foreach (var decision in new[] { "Dismissed", "ActionTaken" })
            {
                using var resolved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/resolve", scenario.Lecturer, role,
                    new ResolveShowcaseReportRequest { Status = decision });
                resolved.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
            using var proposed = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, role,
                new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
            proposed.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(x => x.Id == reportId)).Status.Should().Be(ShowcaseReportStatus.Open);
            (await scenario.Db.ShowcaseModerationRuleProposals.AnyAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();

            await scenario.Db.ShowcaseReports.Where(x => x.Id == reportId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, ShowcaseReportStatus.Dismissed));
            using var deleted = await scenario.SendAsync(HttpMethod.Delete, $"/api/showcase/reports/{reportId}", scenario.Lecturer, role);
            deleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(x => x.Id == reportId)).Status.Should().Be(ShowcaseReportStatus.Dismissed);
            (await scenario.Db.OutboxMessages.CountAsync(x => x.AggregateId == scenario.Page.Id)).Should().Be(notificationCount);
        }
        finally
        {
            // Restore only this disposable fixture so its normal cleanup can find the page.
            await scenario.Db.ShowcaseProjects.IgnoreQueryFilters().Where(x => x.Id == scenario.Page.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsDeleted, false));
        }
    }

    [Fact]
    public async Task RuleStatus_ControlsBlocking_AndLegacyRequestsRemainCompatible()
    {
        await using var scenario = await SeedAsync();
        var phrase = "managed vocabulary " + Guid.NewGuid().ToString("N");
        using var created = await scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Lecturer, SystemRoles.Admin,
            new SaveShowcaseModerationRuleRequest { Phrase = phrase, Description = "Status lifecycle", Category = "Insult", Status = "Active" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var rule = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!;
        try
        {
            rule.Status.Should().Be("Active"); rule.IsEnabled.Should().BeTrue();
            using var blocked = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Reporter, SystemRoles.Student,
                new CreateShowcaseCommentRequest { Content = phrase });
            blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var deactivated = await scenario.SendAsync(HttpMethod.Put, $"/api/showcase/moderation-rules/{rule.Id}", scenario.Lecturer, SystemRoles.Admin,
                new SaveShowcaseModerationRuleRequest { Phrase = phrase, Description = "Status lifecycle", Status = "Inactive", RowVersion = rule.RowVersion });
            deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
            var inactive = (await deactivated.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!;
            inactive.Status.Should().Be("Inactive"); inactive.IsEnabled.Should().BeFalse();
            using var allowed = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.Reporter, SystemRoles.Student,
                new CreateShowcaseCommentRequest { Content = phrase });
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
            using var descriptionOnly = await scenario.SendAsync(HttpMethod.Put, $"/api/showcase/moderation-rules/{rule.Id}", scenario.Lecturer, SystemRoles.Admin,
                new SaveShowcaseModerationRuleRequest { Phrase = phrase, Description = "Revised description", RowVersion = inactive.RowVersion });
            descriptionOnly.StatusCode.Should().Be(HttpStatusCode.OK);
            var revised = (await descriptionOnly.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!;
            revised.Status.Should().Be("Inactive");
            using var legacy = await scenario.SendAsync(HttpMethod.Put, $"/api/showcase/moderation-rules/{rule.Id}", scenario.Lecturer, SystemRoles.Admin,
                new SaveShowcaseModerationRuleRequest { Phrase = phrase, Description = revised.Description, IsEnabled = true, RowVersion = revised.RowVersion });
            legacy.StatusCode.Should().Be(HttpStatusCode.OK);
            (await legacy.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!.Status.Should().Be("Active");
            using var blockedAgain = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{scenario.Page.Id}/comments", scenario.RateReporter, SystemRoles.Student,
                new CreateShowcaseCommentRequest { Content = phrase });
            blockedAgain.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally { await scenario.Db.ShowcaseModerationRules.Where(x => x.Id == rule.Id).ExecuteDeleteAsync(); }
    }

    [Theory]
    [InlineData("ProhibitedContent", "InappropriateContent")]
    [InlineData("Misinformation", "Other")]
    [InlineData("OffTopic", "Other")]
    public async Task ManualCategories_CanBeReported_ButCannotCreateRulesOrRuleProposals(string category, string reason)
    {
        await using var scenario = await SeedAsync();
        using var created = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Category = category, Reason = reason });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var reportId = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
        using var queue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open", scenario.Lecturer, SystemRoles.Lecturer);
        (await queue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Single(x => x.Id == reportId).Category.Should().Be(category);
        var phrase = "context dependent " + Guid.NewGuid().ToString("N");
        using var forbiddenRule = await scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Lecturer, SystemRoles.Admin,
            new SaveShowcaseModerationRuleRequest { Category = category, Phrase = phrase, Description = "Requires context", Status = "Active" });
        forbiddenRule.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var commentReportId = await SeedProposalCommentAsync(scenario);
        using var forbiddenProposal = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{commentReportId}/rule-proposals", scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = category });
        forbiddenProposal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await scenario.Db.ShowcaseModerationRules.AnyAsync(x => x.Phrase == phrase)).Should().BeFalse();
        (await scenario.Db.ShowcaseModerationRuleProposals.AnyAsync(x => x.ShowcaseProjectId == scenario.Page.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentAdminRuleCreation_DoesNotDuplicateThePhrase()
    {
        await using var scenario = await SeedAsync();
        var phrase = "concurrent managed " + Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Lecturer, SystemRoles.Admin,
            new SaveShowcaseModerationRuleRequest { Phrase = phrase, Description = "Concurrent create", Status = "Active", Category = "Insult" })));
        try
        {
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            (await scenario.Db.ShowcaseModerationRules.CountAsync(x => x.Phrase == phrase)).Should().Be(1);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
            await scenario.Db.ShowcaseModerationRules.Where(x => x.Phrase == phrase).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task RuleAudit_RecordsAuthenticatedActors_AndPreservesCreationOnUpdate()
    {
        await using var scenario = await SeedAsync();
        using var created = await scenario.SendAsync(HttpMethod.Post, "/api/showcase/moderation-rules", scenario.Lecturer, SystemRoles.Admin,
            new SaveShowcaseModerationRuleRequest { Phrase = "audit " + Guid.NewGuid().ToString("N"), Description = "Audit test" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = (await created.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!;
        try
        {
            first.CreatedByUserId.Should().Be(scenario.Lecturer.Id);
            first.CreatedByName.Should().Be(scenario.Lecturer.FullName);
            first.CreatedAtUtc.Should().NotBeNull();
            first.UpdatedAtUtc.Should().Be(first.CreatedAtUtc);
            // PostgreSQL timestamps keep microseconds; compare preservation against the persisted value,
            // rather than the extra 100ns digits in the immediate .NET creation receipt.
            var initialRow = await scenario.Db.ShowcaseModerationRules.AsNoTracking().SingleAsync(x => x.Id == first.Id);
            first.CreatedAtUtc.Should().BeCloseTo(initialRow.CreatedAtUtc!.Value, TimeSpan.FromMicroseconds(1));
            using var updated = await scenario.SendAsync(HttpMethod.Put, $"/api/showcase/moderation-rules/{first.Id}", scenario.Reporter, SystemRoles.Admin,
                new SaveShowcaseModerationRuleRequest { Phrase = first.Phrase, Description = "Updated", Status = "Inactive", RowVersion = first.RowVersion });
            updated.StatusCode.Should().Be(HttpStatusCode.OK);
            var second = (await updated.Content.ReadFromJsonAsync<ApiResponse<ShowcaseModerationRuleDto>>())!.Data!;
            second.CreatedAtUtc.Should().Be(initialRow.CreatedAtUtc); second.CreatedByUserId.Should().Be(first.CreatedByUserId);
            second.UpdatedByUserId.Should().Be(scenario.Reporter.Id); second.UpdatedByName.Should().Be(scenario.Reporter.FullName);
            second.UpdatedAtUtc.Should().BeOnOrAfter(initialRow.CreatedAtUtc.Value);
            var stored = await scenario.Db.ShowcaseModerationRules.AsNoTracking().SingleAsync(x => x.Id == first.Id);
            stored.UpdatedByUserId.Should().Be(scenario.Reporter.Id); stored.Status.Should().Be(ShowcaseModerationRuleStatus.Inactive);
        }
        finally { await scenario.Db.ShowcaseModerationRules.Where(x => x.Id == first.Id).ExecuteDeleteAsync(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReportSnapshot_IsPersistedBeforeChanges_AndOnlyReturnedToScopedStaff(bool isComment)
    {
        await using var scenario = await SeedAsync();
        var comment = new ShowcaseComment { ShowcaseProjectId = scenario.Page.Id, AuthorUserId = scenario.Owner.Id, Content = "Original snapshot text" };
        scenario.Db.ShowcaseComments.Add(comment); await scenario.Db.SaveChangesAsync();
        var body = new CreateShowcaseReportRequest { CommentId = isComment ? comment.Id : null, Reason = "Other" };
        using var posted = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student, body);
        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await posted.Content.ReadAsStringAsync()).Should().NotContain("contentSnapshot");
        var reportId = (await posted.Content.ReadFromJsonAsync<ApiResponse<ShowcaseReportReceiptDto>>())!.Data!.Id;
        var original = (await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(x => x.Id == reportId)).ContentSnapshot;
        original.Should().NotBeNullOrEmpty();
        if (isComment) original.Should().Be(comment.Content);
        else
        {
            using var snapshot = System.Text.Json.JsonDocument.Parse(original!);
            snapshot.RootElement.GetProperty("ProjectName").GetString().Should().Be(scenario.Project.Name);
        }
        comment.Content = "Changed content"; comment.IsDeleted = true;
        scenario.Project.Name = "Changed project name"; scenario.Page.Highlights = "Changed highlights";
        await scenario.Db.SaveChangesAsync();
        using var queue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open", scenario.Lecturer, SystemRoles.Lecturer);
        var item = (await queue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Single(x => x.Id == reportId);
        item.ContentSnapshot.Should().Be(original);
        if (isComment) item.CommentContent.Should().Be(comment.Content);
        using var outside = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open", scenario.Reporter, SystemRoles.Lecturer);
        outside.StatusCode.Should().Be(HttpStatusCode.OK);
        (await outside.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Should().NotContain(x => x.Id == reportId);
        using var student = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports?status=Open", scenario.Owner, SystemRoles.Student);
        student.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var duplicate = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student, body);
        duplicate.StatusCode.Should().Be(isComment ? HttpStatusCode.NotFound : HttpStatusCode.Conflict);
        (await scenario.Db.ShowcaseReports.AsNoTracking().SingleAsync(x => x.Id == reportId)).ContentSnapshot.Should().Be(original);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task ProposalReview_PersistsPrivateNotes_AndRejectsOversizedNotesWithoutWrites(string status)
    {
        await using var scenario = await SeedAsync();
        var reportId = await SeedProposalCommentAsync(scenario);
        using var proposed = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{reportId}/rule-proposals", scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" });
        var proposal = (await proposed.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        var path = $"/api/showcase/moderation-rule-proposals/{proposal.Id}/review";
        using var invalid = await scenario.SendAsync(HttpMethod.Post, path, scenario.Reporter, SystemRoles.Admin,
            new ReviewShowcaseRuleProposalRequest { Status = status, RowVersion = proposal.RowVersion, ReviewNote = new string('a', 1001) });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await scenario.Db.ShowcaseModerationRuleProposals.AsNoTracking().SingleAsync(x => x.Id == proposal.Id)).Status.Should().Be(ShowcaseRuleProposalStatus.Pending);
        var note = new string('a', 1000);
        using var reviewed = await scenario.SendAsync(HttpMethod.Post, path, scenario.Reporter, SystemRoles.Admin,
            new ReviewShowcaseRuleProposalRequest { Status = status, RowVersion = proposal.RowVersion, ReviewNote = note });
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await reviewed.Content.ReadFromJsonAsync<ApiResponse<ShowcaseRuleProposalDto>>())!.Data!;
        result.ReviewNote.Should().Be(note);
        using var history = await scenario.SendAsync(HttpMethod.Get, $"/api/showcase/moderation-rule-proposals?status={status}", scenario.Reporter, SystemRoles.Admin);
        (await history.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseRuleProposalDto>>>())!.Data!.Items.Single(x => x.Id == proposal.Id).ReviewNote.Should().Be(note);
        (await scenario.Db.OutboxMessages.Where(x => x.AggregateId == scenario.Page.Id).Select(x => x.PayloadJson).ToListAsync()).Should().OnlyContain(payload => !payload.Contains(note));
        if (status == "Approved")
        {
            var rule = await scenario.Db.ShowcaseModerationRules.AsNoTracking().SingleAsync(x => x.Id == result.CreatedRuleId);
            rule.CreatedByUserId.Should().Be(scenario.Reporter.Id); rule.CreatedAtUtc.Should().NotBeNull();
        }
    }

    private async Task<Scenario> SeedAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        User User(string name) => new() { FullName = name, Email = $"report-{name}-{marker}@example.test", NormalizedEmail = $"REPORT-{name}-{marker}@example.test".ToUpperInvariant() };
        var owner = User("Owner");
        var lecturer = User("Lecturer");
        var reporter = User("Reporter");
        var rateReporter = User("RateReporter");
        db.Users.AddRange(owner, lecturer, reporter, rateReporter);
        var courseId = await db.Courses.Select(item => item.Id).FirstAsync();
        var semesterId = await db.Semesters.Select(item => item.Id).FirstAsync();
        var index = (await db.Classes.IgnoreQueryFilters().Where(item => item.CourseId == courseId && item.SemesterId == semesterId)
            .Select(item => (int?)item.ClassIndex).MaxAsync() ?? 0) + 1;
        var classroom = new Class
        {
            ClassCode = $"Report-{marker}", Slug = $"report-{marker}", ClassIndex = index,
            CourseId = courseId, SemesterId = semesterId, PrimaryLecturerId = lecturer.Id
        };
        var student = new Student { UserId = owner.Id, FullName = "Report owner" };
        var enrollment = new ClassStudent { Class = classroom, Student = student, EnrollmentStatus = EnrollmentStatus.Active };
        var team = new Team { Class = classroom, TeamCode = marker[..8], TeamName = "Report team" };
        team.TeamMembers.Add(new TeamMember { Team = team, ClassStudent = enrollment, ClassId = classroom.Id, StudentId = student.Id, CountsTowardActiveTeam = true });
        var project = new Project { Team = team, Name = $"Report {marker}", Description = "Report integration fixture" };
        var page = new ShowcaseProject
        {
            ProjectLineageId = project.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow,
            IsFeatured = true, FeaturedAt = DateTime.UtcNow
        };
        db.Projects.Add(project);
        db.ShowcaseProjects.Add(page);
        await db.SaveChangesAsync();
        return new Scenario(scope, db, factory.CreateClient(), page, project, classroom, student, owner, lecturer, reporter, rateReporter);
    }

    private sealed class ResolveBetweenReadsInterceptor(Func<Task> resolveWinner) : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            var filtersOpen = command.CommandText.Contains("'Open'", StringComparison.Ordinal) ||
                command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value?.ToString() == "Open");
            if (!Triggered && filtersOpen && command.CommandText.Contains("showcase_reports", StringComparison.Ordinal) &&
                command.CommandText.Contains("comment_id", StringComparison.Ordinal))
            {
                Triggered = true;
                await resolveWinner();
            }
            return result;
        }
    }

    private sealed record Scenario(
        AsyncServiceScope Scope, AppDbContext Db, HttpClient Client, ShowcaseProject Page,
        Project Project, Class Classroom, Student Student, User Owner, User Lecturer, User Reporter, User RateReporter) : IAsyncDisposable
    {
        public string CreatePath => $"/api/showcase/{Page.Id}/reports";

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, User user, string role, object? body = null)
        {
            var tokens = Scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(user, [role]).Token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Db.ChangeTracker.Clear();
            var createdRuleIds = await Db.ShowcaseModerationRuleProposals.Where(x => x.ShowcaseProjectId == Page.Id && x.CreatedRuleId != null).Select(x => x.CreatedRuleId!.Value).ToListAsync();
            await Db.ShowcaseModerationRuleProposals.Where(x => x.ShowcaseProjectId == Page.Id).ExecuteDeleteAsync();
            await Db.ShowcaseModerationRules.Where(x => createdRuleIds.Contains(x.Id)).ExecuteDeleteAsync();
            await Db.ShowcaseReports.Where(item => item.ShowcaseProjectId == Page.Id).ExecuteDeleteAsync();
            await Db.ShowcaseComments.IgnoreQueryFilters().Where(item => item.ShowcaseProjectId == Page.Id).ExecuteDeleteAsync();
            await Db.OutboxMessages.Where(item => item.AggregateId == Page.Id && item.AggregateType == "Showcase").ExecuteDeleteAsync();
            await Db.TeamMembers.Where(item => item.TeamId == Project.TeamId).ExecuteDeleteAsync();
            await Db.ClassStudents.Where(item => item.ClassId == Classroom.Id).ExecuteDeleteAsync();
            Db.ShowcaseProjects.Remove(await Db.ShowcaseProjects.SingleAsync(item => item.Id == Page.Id));
            Db.Projects.Remove(await Db.Projects.SingleAsync(item => item.Id == Project.Id));
            Db.Teams.Remove(await Db.Teams.SingleAsync(item => item.Id == Project.TeamId));
            Db.Classes.Remove(await Db.Classes.SingleAsync(item => item.Id == Classroom.Id));
            Db.Students.Remove(await Db.Students.SingleAsync(item => item.Id == Student.Id));
            var userIds = new[] { Owner.Id, Lecturer.Id, Reporter.Id, RateReporter.Id };
            Db.Users.RemoveRange(await Db.Users.Where(item => userIds.Contains(item.Id)).ToListAsync());
            await Db.SaveChangesAsync();
            Client.Dispose();
            await Scope.DisposeAsync();
        }
    }

}
