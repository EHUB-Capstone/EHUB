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
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task LegacySystemFlags_AreHiddenFromTheTeacherQueue_AndCannotBeResolved()
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
        using var otherClass = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{report.Id}/resolve", scenario.Reporter,
            SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        otherClass.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var resolved = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/reports/{report.Id}/resolve", scenario.Lecturer,
            SystemRoles.Lecturer, new ResolveShowcaseReportRequest { Status = "ActionTaken" });
        resolved.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var queue = await scenario.SendAsync(HttpMethod.Get, "/api/showcase/reports", scenario.Lecturer, SystemRoles.Lecturer);
        (await queue.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseReportDto>>>())!.Data!.Items.Should().NotContain(x => x.Id == report.Id);
        scenario.Db.ChangeTracker.Clear();
        (await scenario.Db.ShowcaseProjects.SingleAsync(x => x.Id == scenario.Page.Id)).Status.Should().Be(ShowcaseStatus.Published);
    }

    [Fact]
    public async Task LegacySystemFlags_RemainNonActionable_AfterContentChanges()
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
