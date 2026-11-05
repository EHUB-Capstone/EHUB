using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Featured;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Migrations;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseFeaturedHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);
    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.Lecturer)]
    public async Task Reviewer_CanFeatureAndUnfeature_Idempotently_WithHistory(string role)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, publishedAt: Now.AddDays(-1));
        var handler = new ShowcaseFeaturedHandler(db, Clock());
        var actor = seed.LecturerUserId;
        var featured = await handler.SetAsync(seed.ShowcaseId, true, actor, role);
        featured.IsSuccess.Should().BeTrue();
        featured.Value.IsFeatured.Should().BeTrue();
        featured.Value.FeaturedAt.Should().Be(Now);
        var history = await db.ShowcaseReviewHistories.SingleAsync();
        history.ActorId.Should().Be(actor);
        history.CreatedAt.Should().Be(Now);
        (history.FromStatus, history.ToStatus).Should().Be((ShowcaseStatus.Published, ShowcaseStatus.Published));
        history.Note.Should().Contain("featured");
        (await handler.SetAsync(seed.ShowcaseId, true, actor, role)).Value.FeaturedAt.Should().Be(Now);
        (await db.ShowcaseReviewHistories.CountAsync()).Should().Be(1);
        var cleared = await handler.SetAsync(seed.ShowcaseId, false, actor, role);
        cleared.Value.IsFeatured.Should().BeFalse();
        cleared.Value.FeaturedAt.Should().BeNull();
        await handler.SetAsync(seed.ShowcaseId, false, actor, role);
        (await db.ShowcaseReviewHistories.CountAsync()).Should().Be(2);
        var page = await db.ShowcaseProjects.FindAsync(seed.ShowcaseId);
        page!.Status.Should().Be(ShowcaseStatus.Published);
        page.PublishedAt.Should().Be(Now.AddDays(-1));
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    public async Task StudentsAndMentors_AreDeniedForBothActions(string role)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var handler = new ShowcaseFeaturedHandler(db, Clock());
        foreach (var flag in new[] { true, false })
            (await handler.SetAsync(seed.ShowcaseId, flag, seed.OwnerUserId, role)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await db.ShowcaseReviewHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task FeaturedTimestamp_MatchesPostgreSqlPrecision_ForStableRetryResponses()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var clock = Clock();
        clock.UtcNow.Returns(Now.AddTicks(7));
        var handler = new ShowcaseFeaturedHandler(db, clock);
        var first = await handler.SetAsync(seed.ShowcaseId, true, seed.LecturerUserId, SystemRoles.Lecturer);
        first.Value.FeaturedAt.Should().Be(Now);
        (await handler.SetAsync(seed.ShowcaseId, true, seed.LecturerUserId, SystemRoles.Lecturer)).Value.FeaturedAt.Should().Be(first.Value.FeaturedAt);
    }

    [Fact]
    public async Task ForeignLecturerAndMissingIds_LookMissing_EvenForDrafts()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db);
        var handler = new ShowcaseFeaturedHandler(db, Clock());
        foreach (var flag in new[] { true, false })
        {
            (await handler.SetAsync(seed.ShowcaseId, flag, Guid.NewGuid(), SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
            (await handler.SetAsync(Guid.NewGuid(), flag, Guid.NewGuid(), SystemRoles.Admin)).Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        }
    }

    [Theory]
    [InlineData(ShowcaseStatus.Draft)]
    [InlineData(ShowcaseStatus.PendingReview)]
    [InlineData(ShowcaseStatus.ChangesRequested)]
    [InlineData(ShowcaseStatus.Hidden)]
    public async Task UnpublishedPages_RejectBothActions(ShowcaseStatus status)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, status);
        var handler = new ShowcaseFeaturedHandler(db, Clock());
        foreach (var flag in new[] { true, false })
            (await handler.SetAsync(seed.ShowcaseId, flag, seed.LecturerUserId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
        (await db.ShowcaseReviewHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ClassLecturerAssignment_CanFeatureWithoutPrimaryLecturerField()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var lecturer = await ShowcaseTestData.AddUserAsync(db, "Assigned lecturer");
        (await db.Classes.FindAsync(seed.ClassId))!.PrimaryLecturerId = null;
        db.ClassLecturers.Add(new ClassLecturer { ClassId = seed.ClassId, LecturerId = lecturer.Id, IsPrimary = true });
        await db.SaveChangesAsync();
        (await new ShowcaseFeaturedHandler(db, Clock()).SetAsync(seed.ShowcaseId, true, lecturer.Id, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HideOrOwnerWithdraw_ClearsFeaturedAndHistory_WithoutRestoringIt(bool withdraw)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, publishedAt: Now.AddDays(-1));
        await new ShowcaseFeaturedHandler(db, Clock()).SetAsync(seed.ShowcaseId, true, seed.LecturerUserId, SystemRoles.Lecturer);
        var moderation = new ShowcaseModerationHandler(db, Clock());
        var result = withdraw
            ? await moderation.WithdrawAsync(seed.ShowcaseId, seed.OwnerUserId, SystemRoles.Student)
            : await moderation.DecideAsync(seed.ShowcaseId, ShowcaseModerationAction.Hide, "Hidden for review", seed.LecturerUserId, SystemRoles.Lecturer);
        result.IsSuccess.Should().BeTrue();
        result.Value.IsFeatured.Should().BeFalse();
        result.Value.FeaturedAt.Should().BeNull();
        result.Value.Status.Should().Be("Hidden");
        (await db.ShowcaseReviewHistories.CountAsync()).Should().Be(3);
        var restored = await moderation.DecideAsync(seed.ShowcaseId, ShowcaseModerationAction.Unhide, null, seed.LecturerUserId, SystemRoles.Lecturer);
        restored.Value.IsFeatured.Should().BeFalse();
        restored.Value.FeaturedAt.Should().BeNull();
    }

    private sealed class ConflictOnSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled) throw new DbUpdateConcurrencyException("Simulated concurrent hide.");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task ConcurrentChange_ReturnsConflictWithoutSavingFeaturedOrHistory()
    {
        var interceptor = new ConflictOnSave();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(interceptor).Options);
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        interceptor.Enabled = true;
        var result = await new ShowcaseFeaturedHandler(db, Clock()).SetAsync(seed.ShowcaseId, true, seed.LecturerUserId, SystemRoles.Lecturer);
        result.Error.Code.Should().Be(ErrorCodes.ShowcaseConcurrencyConflict);
        (await db.ShowcaseProjects.AsNoTracking().SingleAsync()).IsFeatured.Should().BeFalse();
        (await db.ShowcaseReviewHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Migration_OnlyAddsFeaturedColumns_AndDownOnlyRemovesThem()
    {
        var migration = new AddShowcaseFeaturedSelection();
        var up = migration.UpOperations.Cast<AddColumnOperation>().ToArray();
        up.Select(column => column.Name).Should().BeEquivalentTo("is_featured", "featured_at");
        up.Should().OnlyContain(column => column.Table == "showcase_projects");
        up.Single(column => column.Name == "is_featured").DefaultValue.Should().Be(false);
        up.Single(column => column.Name == "featured_at").IsNullable.Should().BeTrue();
        migration.DownOperations.Cast<DropColumnOperation>().Should().HaveCount(2).And.OnlyContain(column => column.Table == "showcase_projects");
    }
}
