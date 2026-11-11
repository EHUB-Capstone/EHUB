using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        (await scenario.Db.ShowcaseComments.IgnoreQueryFilters().SingleAsync(item => item.Id == comment.Id))
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
        await scenario.Db.ShowcaseProjects.Where(item => item.Id == scenario.Page.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ShowcaseStatus.Hidden));
        using var hidden = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Spam" });
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        for (var i = 0; i < 4; i++)
        {
            using var allowed = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
                new CreateShowcaseReportRequest { Reason = "Spam" });
            allowed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        using var limited = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Reporter, SystemRoles.Student,
            new CreateShowcaseReportRequest { Reason = "Spam" });
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        using var independent = await scenario.SendAsync(HttpMethod.Post, scenario.CreatePath, scenario.Lecturer, SystemRoles.Lecturer,
            new CreateShowcaseReportRequest { Reason = "Spam" });
        independent.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        db.Users.AddRange(owner, lecturer, reporter);
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
        return new Scenario(scope, db, factory.CreateClient(), page, project, classroom, student, owner, lecturer, reporter);
    }

    private sealed record Scenario(
        AsyncServiceScope Scope, AppDbContext Db, HttpClient Client, ShowcaseProject Page,
        Project Project, Class Classroom, Student Student, User Owner, User Lecturer, User Reporter) : IAsyncDisposable
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
            var userIds = new[] { Owner.Id, Lecturer.Id, Reporter.Id };
            Db.Users.RemoveRange(await Db.Users.Where(item => userIds.Contains(item.Id)).ToListAsync());
            await Db.SaveChangesAsync();
            Client.Dispose();
            await Scope.DisposeAsync();
        }
    }

}
