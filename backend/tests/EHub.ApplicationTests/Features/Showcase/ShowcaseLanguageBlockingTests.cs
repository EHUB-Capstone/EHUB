using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Showcase.Manage;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Application.Features.Showcase.Reports;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseLanguageBlockingTests
{
    [Theory]
    [InlineData(ShowcaseStatus.Draft)]
    [InlineData(ShowcaseStatus.Published)]
    public async Task UpdatingShowcase_RejectsBadHighlightsAndLinks_WithoutChangingContentOrCreatingReports(ShowcaseStatus status)
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        var seed = await ShowcaseTestData.SeedAsync(db, status);
        var handler = new ShowcaseManageHandler(db, Substitute.For<IImageStorageService>(), Substitute.For<IDateTimeProvider>(), new SaveShowcaseRequestValidator());
        foreach (var request in new[] {
            (Request: new SaveShowcaseRequest { Highlights = "cdmm" }, Code: ErrorCodes.ShowcaseLanguageProfanity),
            (Request: new SaveShowcaseRequest { Links = [new ShowcaseLinkRequest { Label = "phim sex", Url = "https://example.test" }] }, Code: ErrorCodes.ShowcaseLanguageAdultContent)
        })
        {
            var result = await handler.UpdateAsync(seed.ShowcaseId, request.Request, seed.OwnerUserId, SystemRoles.Student);
            result.IsFailure.Should().BeTrue(); result.Error.Code.Should().Be(request.Code);
            (await db.ShowcaseProjects.AsNoTracking().SingleAsync()).Highlights.Should().BeNull();
            (await db.ShowcaseLinks.CountAsync()).Should().Be(0);
            (await db.ShowcaseReports.CountAsync()).Should().Be(0);
            (await db.OutboxMessages.CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task SubmitAndApprove_RejectLegacyOffensiveProfile_WithoutTransitioning()
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        var seed = await ShowcaseTestData.SeedAsync(db);
        (await db.Projects.SingleAsync()).Description = "tao sẽ giết mày";
        await db.SaveChangesAsync();
        var handler = new ShowcaseModerationHandler(db, Substitute.For<IDateTimeProvider>());
        var submit = await handler.SubmitAsync(seed.ShowcaseId, new SubmitShowcaseRequest { AcknowledgedPublishWarning = true }, seed.OwnerUserId, SystemRoles.Student);
        submit.IsFailure.Should().BeTrue(); submit.Error.Code.Should().Be(ErrorCodes.ShowcaseLanguageThreat);
        (await db.ShowcaseProjects.SingleAsync()).Status.Should().Be(ShowcaseStatus.Draft);
        (await db.ShowcaseProjects.SingleAsync()).Status = ShowcaseStatus.PendingReview; await db.SaveChangesAsync();
        var approve = await handler.DecideAsync(seed.ShowcaseId, ShowcaseModerationAction.Approve, null, seed.LecturerUserId, SystemRoles.Lecturer);
        approve.IsFailure.Should().BeTrue(); approve.Error.Code.Should().Be(ErrorCodes.ShowcaseLanguageThreat);
        (await db.ShowcaseProjects.SingleAsync()).Status.Should().Be(ShowcaseStatus.PendingReview);
        (await db.ShowcaseReviewHistories.CountAsync()).Should().Be(0);
        (await db.ShowcaseReports.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProfileGuard_OnlyAppliesToProjectsThatHaveAShowcase()
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        await ShowcaseTestData.SeedAsync(db);
        var project = await db.Projects.SingleAsync();
        (await ShowcaseLanguageGuard.ValidateProfileIfShowcasedAsync(db, project, ["fuck"], default)).IsFailure.Should().BeTrue();
        db.ShowcaseProjects.Remove(await db.ShowcaseProjects.SingleAsync()); await db.SaveChangesAsync();
        (await ShowcaseLanguageGuard.ValidateProfileIfShowcasedAsync(db, project, ["fuck"], default)).IsSuccess.Should().BeTrue();
    }
}
