using EHub.Application.Features.Showcase.Query;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseQueryHandlerTests
{
    [Fact]
    public async Task PublishedPage_IsVisibleToAnySignedInUser_WithoutInternalReviewData()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, publishedAt: DateTime.UtcNow);
        await AddHistoryAsync(db, seed.ShowcaseId, seed.LecturerUserId);
        var page = await db.ShowcaseProjects.FindAsync(seed.ShowcaseId);
        page!.ReviewNote = "Internal remark";
        await db.SaveChangesAsync();

        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor, SystemRoles.Lecturer, SystemRoles.Admin })
        {
            var result = await new ShowcaseQueryHandler(db).GetAsync(seed.ShowcaseId, Guid.NewGuid(), role);

            result.IsSuccess.Should().BeTrue(because: role);
            result.Value.Showcase.Profile.ProjectName.Should().Be("Smart Garden");
            result.Value.TeamName.Should().Be("Garden Team");
            result.Value.IsOwner.Should().BeFalse();
        }

        var student = await new ShowcaseQueryHandler(db).GetAsync(seed.ShowcaseId, Guid.NewGuid(), SystemRoles.Student);
        student.Value.Showcase.ReviewNote.Should().BeNull();
        student.Value.History.Should().BeEmpty();
        student.Value.Showcase.CanEdit.Should().BeFalse();
    }

    [Theory]
    [InlineData(ShowcaseStatus.Draft)]
    [InlineData(ShowcaseStatus.PendingReview)]
    [InlineData(ShowcaseStatus.ChangesRequested)]
    [InlineData(ShowcaseStatus.Hidden)]
    public async Task UnpublishedPage_LooksMissingToEveryoneOutsideTheTeamAndReviewScope(ShowcaseStatus status)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, status);
        var otherLecturer = await ShowcaseTestData.AddUserAsync(db, "Other lecturer");
        var handler = new ShowcaseQueryHandler(db);

        var student = await handler.GetAsync(seed.ShowcaseId, Guid.NewGuid(), SystemRoles.Student);
        var mentor = await handler.GetAsync(seed.ShowcaseId, Guid.NewGuid(), SystemRoles.Mentor);
        var lecturer = await handler.GetAsync(seed.ShowcaseId, otherLecturer.Id, SystemRoles.Lecturer);

        student.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        mentor.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        lecturer.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
    }

    [Fact]
    public async Task Owner_SeesTheirDraft_WithReviewNoteAndHistory()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.ChangesRequested, showTeamMembers: false);
        var page = await db.ShowcaseProjects.FindAsync(seed.ShowcaseId);
        page!.ReviewNote = "Add a demo link.";
        await AddHistoryAsync(db, seed.ShowcaseId, seed.LecturerUserId);

        var result = await new ShowcaseQueryHandler(db).GetAsync(seed.ShowcaseId, seed.OwnerUserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsOwner.Should().BeTrue();
        result.Value.Showcase.CanEdit.Should().BeTrue();
        result.Value.Showcase.ReviewNote.Should().Be("Add a demo link.");
        result.Value.History.Should().ContainSingle().Which.ActorName.Should().Be("Lecturer");
        result.Value.Members.Should().ContainSingle(); // owners always see their own team
    }

    [Fact]
    public async Task AssignedLecturerAndAdmin_CanReviewAPendingPage()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.PendingReview);
        await AddHistoryAsync(db, seed.ShowcaseId, seed.LecturerUserId);
        var handler = new ShowcaseQueryHandler(db);

        var lecturer = await handler.GetAsync(seed.ShowcaseId, seed.LecturerUserId, SystemRoles.Lecturer);
        var admin = await handler.GetAsync(seed.ShowcaseId, Guid.NewGuid(), SystemRoles.Admin);

        lecturer.Value.CanModerate.Should().BeTrue();
        lecturer.Value.IsOwner.Should().BeFalse();
        lecturer.Value.History.Should().HaveCount(1);
        admin.Value.CanModerate.Should().BeTrue();
    }

    [Fact]
    public async Task TeamMembers_AreHiddenFromTheCommunity_WhenTheTeamOptedOut()
    {
        await using var db = ShowcaseTestData.NewContext();
        var hidden = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, showTeamMembers: false, publishedAt: DateTime.UtcNow);
        var shown = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, showTeamMembers: true, publishedAt: DateTime.UtcNow);
        var handler = new ShowcaseQueryHandler(db);

        var optedOut = await handler.GetAsync(hidden.ShowcaseId, Guid.NewGuid(), SystemRoles.Student);
        var optedIn = await handler.GetAsync(shown.ShowcaseId, Guid.NewGuid(), SystemRoles.Student);
        var reviewer = await handler.GetAsync(hidden.ShowcaseId, hidden.LecturerUserId, SystemRoles.Lecturer);

        optedOut.Value.Members.Should().BeEmpty();
        optedIn.Value.Members.Should().ContainSingle().Which.FullName.Should().Be("Owner Student");
        reviewer.Value.Members.Should().ContainSingle();
    }

    [Fact]
    public async Task MissingPage_IsNotFound()
    {
        await using var db = ShowcaseTestData.NewContext();

        var result = await new ShowcaseQueryHandler(db).GetAsync(Guid.NewGuid(), Guid.NewGuid(), SystemRoles.Admin);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
    }

    private static async Task AddHistoryAsync(EHub.Infrastructure.Persistence.AppDbContext db, Guid showcaseId, Guid actorId)
    {
        db.ShowcaseReviewHistories.Add(new ShowcaseReviewHistory
        {
            ShowcaseProjectId = showcaseId,
            FromStatus = ShowcaseStatus.Draft,
            ToStatus = ShowcaseStatus.PendingReview,
            Note = "Submitted for review.",
            ActorId = actorId
        });
        await db.SaveChangesAsync();
    }
}
