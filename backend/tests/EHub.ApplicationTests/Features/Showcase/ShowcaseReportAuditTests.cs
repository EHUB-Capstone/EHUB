using System.Text.Json;
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

public sealed class ShowcaseReportAuditTests
{
    [Fact]
    public async Task RuleAudit_PreservesCreator_TracksEditor_AndDoesNotChangeOnConflict()
    {
        await using var db = ShowcaseTestData.NewContext();
        var creator = await ShowcaseTestData.AddUserAsync(db, "First administrator");
        var editor = await ShowcaseTestData.AddUserAsync(db, "Second administrator");
        var clock = ShowcaseTestData.RuleClock();
        var createdAt = clock.UtcNow;
        var handler = new ShowcaseModerationRuleHandler(db, new SaveShowcaseModerationRuleRequestValidator(), ShowcaseTestData.RuleTransaction(), clock);
        var created = (await handler.SaveAsync(null, new() { Phrase = "audit phrase", Description = "Initial" }, creator.Id, SystemRoles.Admin, default)).Value;
        created.CreatedByUserId.Should().Be(creator.Id);
        created.CreatedByName.Should().Be(creator.FullName);
        created.CreatedAtUtc.Should().Be(createdAt);
        clock.UtcNow.Returns(createdAt.AddHours(1));
        var updated = (await handler.SaveAsync(created.Id, new() { Phrase = created.Phrase, Description = "Revised", Status = "Inactive", RowVersion = created.RowVersion }, editor.Id, SystemRoles.Admin, default)).Value;
        updated.CreatedByUserId.Should().Be(creator.Id);
        updated.CreatedAtUtc.Should().Be(createdAt);
        updated.UpdatedByUserId.Should().Be(editor.Id);
        updated.UpdatedByName.Should().Be(editor.FullName);
        updated.UpdatedAtUtc.Should().Be(createdAt.AddHours(1));
        clock.UtcNow.Returns(createdAt.AddDays(1));
        var conflict = await handler.SaveAsync(created.Id, new() { Phrase = created.Phrase, Description = "Stale edit", RowVersion = updated.RowVersion + 1 }, creator.Id, SystemRoles.Admin, default);
        conflict.Error.Code.Should().Be(ErrorCodes.ShowcaseConcurrencyConflict);
        var stored = (await handler.ListAsync(SystemRoles.Admin, default)).Value.Single();
        stored.UpdatedAtUtc.Should().Be(updated.UpdatedAtUtc);
        stored.UpdatedByUserId.Should().Be(editor.Id);
        stored.Description.Should().Be("Revised");
    }

    [Fact]
    public async Task LegacyRuleEdit_DoesNotInventTheOriginalCreatorOrTime()
    {
        await using var db = ShowcaseTestData.NewContext();
        var actor = await ShowcaseTestData.AddUserAsync(db, "Administrator");
        var legacy = new ShowcaseModerationRule { Phrase = "legacy phrase", Description = "Legacy" };
        db.ShowcaseModerationRules.Add(legacy); await db.SaveChangesAsync();
        var clock = ShowcaseTestData.RuleClock();
        var handler = new ShowcaseModerationRuleHandler(db, new SaveShowcaseModerationRuleRequestValidator(), ShowcaseTestData.RuleTransaction(), clock);
        var saved = (await handler.SaveAsync(legacy.Id, new() { Phrase = legacy.Phrase, Description = "Updated", RowVersion = legacy.Version }, actor.Id, SystemRoles.Admin, default)).Value;
        saved.CreatedByUserId.Should().BeNull(); saved.CreatedAtUtc.Should().BeNull();
        saved.UpdatedByUserId.Should().Be(actor.Id); saved.UpdatedAtUtc.Should().Be(clock.UtcNow);
        (await handler.SaveAsync(null, new() { Phrase = "unknown actor", Description = "Test" }, Guid.NewGuid(), SystemRoles.Admin, default))
            .Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("Approved", 1000, true)]
    [InlineData("Rejected", 1000, true)]
    [InlineData("Approved", 1001, false)]
    [InlineData("Rejected", 1001, false)]
    public async Task ProposalReview_ValidatesNotes_AndRecordsTheApprovingAdministrator(string status, int length, bool valid)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var admin = await ShowcaseTestData.AddUserAsync(db, "Approving administrator");
        var comment = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = seed.OwnerUserId, Content = "audit phrase" };
        var proposal = new ShowcaseModerationRuleProposal { ShowcaseProjectId = seed.ShowcaseId, CommentId = comment.Id, Phrase = comment.Content,
            PhraseKey = ShowcaseContentFilter.Hash(comment.Content), Category = ShowcaseViolationCategory.Insult,
            ProposedByUserId = seed.LecturerUserId, ProposedAtUtc = ShowcaseTestData.RuleClock().UtcNow };
        db.ShowcaseComments.Add(comment); db.ShowcaseModerationRuleProposals.Add(proposal); await db.SaveChangesAsync();
        var transaction = Substitute.For<IShowcaseRuleProposalTransaction>();
        transaction.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<EHub.Shared.Results.Result<ShowcaseRuleProposalDto>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<EHub.Shared.Results.Result<ShowcaseRuleProposalDto>>>>()!(call.Arg<CancellationToken>()));
        var handler = new ShowcaseRuleProposalHandler(db, ShowcaseTestData.RuleClock(), new CreateShowcaseRuleProposalValidator(), transaction);
        var note = " " + new string('a', length - 2) + " ";
        var result = await handler.ReviewAsync(proposal.Id, new() { Status = status, RowVersion = proposal.Version, ReviewNote = note }, admin.Id, SystemRoles.Admin);
        result.IsSuccess.Should().Be(valid);
        if (!valid)
        {
            result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
            proposal.Status.Should().Be(ShowcaseRuleProposalStatus.Pending); proposal.ReviewNote.Should().BeNull();
        }
        else
        {
            result.Value.ReviewNote.Should().Be(note.Trim());
            (await handler.ListAsync(status, 1, 20, admin.Id, SystemRoles.Admin)).Value.Items.Single().ReviewNote.Should().Be(note.Trim());
            proposal.ReviewedByUserId.Should().Be(admin.Id);
        }
        if (valid && status == "Approved")
        {
            var rule = await db.ShowcaseModerationRules.SingleAsync();
            rule.CreatedByUserId.Should().Be(admin.Id); rule.CreatedAtUtc.Should().Be(ShowcaseTestData.RuleClock().UtcNow);
            rule.UpdatedByUserId.Should().Be(admin.Id); rule.UpdatedAtUtc.Should().Be(rule.CreatedAtUtc);
        }
        else (await db.ShowcaseModerationRules.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReportSnapshot_SurvivesContentChanges_AndIsNotInThePublicReceipt(bool isComment)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        var comment = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = seed.OwnerUserId, Content = "Original Vietnamese: bò\nSecond line" };
        db.ShowcaseComments.Add(comment); await db.SaveChangesAsync();
        var clock = ShowcaseTestData.RuleClock();
        var handler = new ShowcaseReportHandler(db, clock, new ShowcaseModerationHandler(db, clock), new CreateShowcaseReportRequestValidator(), new ResolveShowcaseReportRequestValidator());
        var receipt = await handler.CreateAsync(seed.ShowcaseId, new() { CommentId = isComment ? comment.Id : null, Reason = "Other" }, reporter.Id);
        receipt.IsSuccess.Should().BeTrue();
        JsonSerializer.Serialize(receipt.Value).Should().NotContain("ContentSnapshot");
        var snapshot = (await db.ShowcaseReports.SingleAsync()).ContentSnapshot;
        snapshot.Should().NotBeNullOrEmpty();
        if (isComment) snapshot.Should().Be(comment.Content);
        else using (var json = JsonDocument.Parse(snapshot!)) json.RootElement.GetProperty("ProjectName").GetString().Should().Be((await db.Projects.SingleAsync()).Name);
        comment.Content = "Changed"; comment.IsDeleted = true;
        (await db.Projects.SingleAsync()).Name = "Changed project name";
        await db.SaveChangesAsync();
        var listed = (await handler.ListAsync("Open", 1, 20, seed.LecturerUserId, SystemRoles.Lecturer)).Value.Items.Single();
        listed.ContentSnapshot.Should().Be(snapshot);
        if (isComment) listed.CommentContent.Should().Be("Changed");
    }
}
