using EHub.Application.Features.Showcase.Reports;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseViolationManagementTests
{
    [Theory]
    [InlineData("Active", null, true)]
    [InlineData("Inactive", null, false)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(null, null, true)]
    [InlineData("Inactive", false, false)]
    public async Task StatusAndLegacyRequests_ControlWhetherAPhraseBlocks(string? status, bool? legacyFlag, bool blocked)
    {
        await using var db = ShowcaseTestData.NewContext();
        var result = await Handler(db).SaveAsync(null, new()
        {
            Phrase = "managed test phrase", Description = "Review context", Category = "Insult", Status = status, IsEnabled = legacyFlag
        }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(blocked ? "Active" : "Inactive");
        result.Value.IsEnabled.Should().Be(blocked);
        (await ShowcaseLanguageGuard.ValidateAsync(db, ["managed test phrase"], default)).IsFailure.Should().Be(blocked);
    }

    [Fact]
    public async Task EditingDescriptionWithoutAStatus_DoesNotReactivateAnInactiveRule()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db);
        var created = await handler.SaveAsync(null, new() { Phrase = "inactive test phrase", Description = "Initial", Status = "Inactive" }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        var edited = await handler.SaveAsync(created.Value.Id, new()
        {
            Phrase = created.Value.Phrase, Description = "Revised", RowVersion = created.Value.RowVersion
        }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        edited.IsSuccess.Should().BeTrue();
        edited.Value.Status.Should().Be("Inactive");
        (await ShowcaseLanguageGuard.ValidateAsync(db, [created.Value.Phrase], default)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Unknown", null)]
    [InlineData("active", null)]
    [InlineData("0", null)]
    [InlineData("", null)]
    [InlineData("Active", false)]
    [InlineData("Inactive", true)]
    public async Task InvalidOrContradictoryStatuses_DoNotWriteRules(string status, bool? legacyFlag)
    {
        await using var db = ShowcaseTestData.NewContext();
        var result = await Handler(db).SaveAsync(null, new()
        {
            Phrase = "test phrase", Description = "Test", Status = status, IsEnabled = legacyFlag
        }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(SystemRoles.Lecturer)]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    public async Task NonAdministrators_CannotReadOrEditTheSharedBlocklist(string role)
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db);
        (await handler.ListAsync(role, default)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await handler.SaveAsync(null, new() { Phrase = "test phrase", Description = "Test", Status = "Active" }, Guid.Empty, role, default))
            .Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DuplicateCanonicalPhrases_AreRejectedWithoutChangingExistingRules()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db);
        var first = await handler.SaveAsync(null, new() { Phrase = "ĐẦN ĐỘN", Description = "First" }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        first.IsSuccess.Should().BeTrue();
        var duplicate = await handler.SaveAsync(null, new() { Phrase = "đần...độn", Description = "Duplicate", Status = "Inactive" }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        duplicate.Error.Code.Should().Be(ErrorCodes.ShowcaseRuleProposalAlreadyExists);
        var stored = await db.ShowcaseModerationRules.SingleAsync();
        stored.Description.Should().Be("First");
        stored.Status.Should().Be(ShowcaseModerationRuleStatus.Active);
    }

    [Theory]
    [InlineData(ShowcaseViolationCategory.ProhibitedContent, "InappropriateContent")]
    [InlineData(ShowcaseViolationCategory.Misinformation, "Other")]
    [InlineData(ShowcaseViolationCategory.OffTopic, "Other")]
    public async Task ContextDependentCategories_AreAvailableForReports_ButCannotBecomeBlockingRules(ShowcaseViolationCategory category, string reason)
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        var catalog = (await new ShowcaseViolationCategoryQueryHandler(db).ListAsync()).Value;
        catalog.Should().HaveCount(10);
        var selected = catalog.Single(x => x.Code == category.ToString());
        selected.SupportsPhraseBlocking.Should().BeFalse();
        selected.DescriptionEn.Should().NotBeEmpty();
        selected.DescriptionVi.Should().NotBeEmpty();
        (await new CreateShowcaseReportRequestValidator().ValidateAsync(new CreateShowcaseReportRequest { Category = category.ToString(), Reason = reason }))
            .IsValid.Should().BeTrue();
        var result = await Handler(db).SaveAsync(null, new() { Phrase = "neutral academic phrase", Description = "Context-dependent", Category = category.ToString() }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        (await new CreateShowcaseRuleProposalValidator().ValidateAsync(new CreateShowcaseRuleProposalRequest { Phrase = "neutral academic phrase", Category = category.ToString() })).IsValid.Should().BeFalse();
        ShowcaseContentFilter.Evaluate(["neutral academic phrase"], [new ShowcaseModerationRule { Phrase = "neutral academic phrase", Category = category }], true).Should().BeEmpty();
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(59);
    }

    private static ShowcaseModerationRuleHandler Handler(AppDbContext db) =>
        new(db, new SaveShowcaseModerationRuleRequestValidator(), ShowcaseTestData.RuleTransaction(), ShowcaseTestData.RuleClock());
}
