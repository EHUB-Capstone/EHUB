using EHub.Application.Features.Showcase.Reports;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseCategoryTests
{
    [Fact]
    public async Task DefaultCatalog_HasTheExpectedCategoryDistribution()
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        var counts = (await db.ShowcaseModerationRules.ToListAsync()).GroupBy(x => x.Category).ToDictionary(x => x.Key, x => x.Count());
        counts.Should().BeEquivalentTo(new Dictionary<ShowcaseViolationCategory, int>
        {
            [ShowcaseViolationCategory.Profanity] = 16,
            [ShowcaseViolationCategory.Insult] = 16,
            [ShowcaseViolationCategory.Threat] = 9,
            [ShowcaseViolationCategory.Defamation] = 4,
            [ShowcaseViolationCategory.Racism] = 6,
            [ShowcaseViolationCategory.AdultContent] = 8
        });
    }

    [Theory]
    [InlineData("con cặc", "PROFANITY", "Profane")]
    [InlineData("ngu ghê", "INSULT", "Insults")]
    [InlineData("tao giết mày", "THREAT", "Threats")]
    [InlineData("đồ lừa đảo", "DEFAMATION", "Abusive accusations")]
    [InlineData("lũ da đen", "RACISM", "Racist")]
    [InlineData("phim sex", "ADULTCONTENT", "adult content")]
    public async Task DefaultRules_ReturnTheActualCategory(string text, string suffix, string message)
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        var result = await ShowcaseLanguageGuard.ValidateAsync(db, [text], default);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShowcaseLanguageBlocked + "_" + suffix);
        result.Error.Message.Should().Contain(message);
    }

    [Fact]
    public async Task MultipleCategories_AreDistinctOrderedAndNotTruncatedAfterFivePhraseMatches()
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        db.ShowcaseModerationRules.Add(new ShowcaseModerationRule { Phrase = "buy spam now", Description = "Test spam", Category = ShowcaseViolationCategory.Spam, Reason = ShowcaseReportReason.Spam });
        await db.SaveChangesAsync();
        var result = await ShowcaseLanguageGuard.ValidateAsync(db,
            ["cdmm clm clgt óc cặc óc loz oc lon fuck dume duma", "ngu", "tao giết mày", "đồ lừa đảo", "lũ da đen", "phim sex", "buy spam now"], default);
        result.Error.Code.Should().Be("SHOWCASE_LANGUAGE_BLOCKED_PROFANITY_INSULT_THREAT_DEFAMATION_RACISM_ADULTCONTENT_SPAM");
        result.Error.Message.Should().Contain("Profane").And.Contain("Insults").And.Contain("Threats").And.Contain("Abusive accusations").And.Contain("Racist").And.Contain("adult content").And.Contain("Spam");
        (await db.ShowcaseReports.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("Profanity", "Harassment")]
    [InlineData("Insult", "Harassment")]
    [InlineData("Threat", "Harassment")]
    [InlineData("Defamation", "Harassment")]
    [InlineData("Racism", "Harassment")]
    [InlineData("AdultContent", "InappropriateContent")]
    [InlineData("Spam", "Spam")]
    public async Task AdminCanSelectCategory_AndLegacyUpdatesPreserveIt(string category, string reason)
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = new ShowcaseModerationRuleHandler(db, new SaveShowcaseModerationRuleRequestValidator(), ShowcaseTestData.RuleTransaction(), ShowcaseTestData.RuleClock());
        var created = await handler.SaveAsync(null, new() { Phrase = "category example", Description = "Test", Category = category }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        created.IsSuccess.Should().BeTrue();
        created.Value.Category.Should().Be(category);
        created.Value.Reason.Should().Be(reason);
        var updated = await handler.SaveAsync(created.Value.Id, new() { Phrase = "updated example", Description = "Updated", Reason = reason, RowVersion = created.Value.RowVersion }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default);
        updated.IsSuccess.Should().BeTrue();
        updated.Value.Category.Should().Be(category);
        (await handler.ListAsync(SystemRoles.Admin, default)).Value.Single().Category.Should().Be(category);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("3")]
    public async Task UnknownCategory_IsRejectedWithoutSaving(string category)
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = new ShowcaseModerationRuleHandler(db, new SaveShowcaseModerationRuleRequestValidator(), ShowcaseTestData.RuleTransaction(), ShowcaseTestData.RuleClock());
        (await handler.SaveAsync(null, new() { Phrase = "test phrase", Description = "Test", Category = category }, (await ShowcaseTestData.AddUserAsync(db, "Admin")).Id, SystemRoles.Admin, default)).IsFailure.Should().BeTrue();
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DisabledRule_DoesNotProduceACategoryError()
    {
        await using var db = ShowcaseTestData.NewContext();
        db.ShowcaseModerationRules.Add(new ShowcaseModerationRule { Phrase = "test phrase", Category = ShowcaseViolationCategory.Threat, Status = ShowcaseModerationRuleStatus.Inactive });
        await db.SaveChangesAsync();
        (await ShowcaseLanguageGuard.ValidateAsync(db, ["test phrase"], default)).IsSuccess.Should().BeTrue();
    }
}
