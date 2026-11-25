using System.Text;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Reports;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Contracts.Showcase;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseRuleProposalTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
    private static IDateTimeProvider Clock() { var clock = Substitute.For<IDateTimeProvider>(); clock.UtcNow.Returns(Now); return clock; }
    private static ShowcaseRuleProposalHandler Handler(IApplicationDbContext db)
    {
        // In-memory unit tests exercise decisions; PostgreSQL integration tests exercise transaction isolation.
        var unit = Substitute.For<IShowcaseRuleProposalTransaction>();
        unit.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<Result<ShowcaseRuleProposalDto>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<ShowcaseRuleProposalDto>>>>()!(call.Arg<CancellationToken>()));
        return new(db, Clock(), new CreateShowcaseRuleProposalValidator(), unit);
    }

    [Theory]
    [InlineData("Bạn là chó!", "chó", true)]
    [InlineData("Bạn là chó!", "CHÓ", true)]
    [InlineData("Bạn là chó!", "cho", false)]
    [InlineData("Bạn là chó!", "chò", false)]
    [InlineData("Bạn là chó!", "chỏ", false)]
    [InlineData("Nguyễn dùng nguyên liệu", "ngu", false)]
    [InlineData("Bạn là đồ đần độn!", "đần độn", true)]
    [InlineData("Bạn là đồ đần độn!", "dan don", false)]
    [InlineData("Số 12345", "12345", false)]
    [InlineData("Bạn là chó!", "", false)]
    [InlineData("Đó là bò!", "bò", true)]
    [InlineData("Đó là bò!", "bo", false)]
    [InlineData("Đó là bò!", "bỏ", false)]
    [InlineData("Quả bóng", "bò", false)]
    public void LiteralSelection_PreservesAccentsAndWordBoundaries(string comment, string phrase, bool expected) =>
        ShowcaseProposalText.IsInComment(comment, phrase).Should().Be(expected);

    [Fact]
    public async Task Pending_DoesNotBlock_AndOnlyApprovalCreatesAnEnabledRule()
    {
        await using var db = ShowcaseTestData.NewContext(); await db.Database.EnsureCreatedAsync();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        var comment = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = reporter.Id, Content = "Bạn là đồ đần độn!" };
        var report = new ShowcaseReport { ShowcaseProjectId = seed.ShowcaseId, CommentId = comment.Id, ReporterUserId = reporter.Id, Reason = ShowcaseReportReason.Harassment, CreatedAt = Now };
        db.ShowcaseComments.Add(comment); db.ShowcaseReports.Add(report); await db.SaveChangesAsync();
        var handler = Handler(db);
        var proposal = await handler.CreateAsync(report.Id, new() { Phrase = "ĐẦN ĐỘN".Normalize(NormalizationForm.FormD), Category = "Insult" }, seed.LecturerUserId, SystemRoles.Lecturer);
        proposal.IsSuccess.Should().BeTrue(); proposal.Value.Status.Should().Be("Pending"); proposal.Value.Phrase.Should().Be("đần độn");
        proposal.Value.ProposedAtUtc.Should().Be(Now);
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(59);
        (await ShowcaseLanguageGuard.ValidateAsync(db, ["đần độn"], default)).IsSuccess.Should().BeTrue();
        (await handler.CreateAsync(report.Id, new() { Phrase = "đần độn", Category = "Insult" }, seed.LecturerUserId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseRuleProposalAlreadyExists);
        (await handler.ReviewAsync(proposal.Value.Id, new() { Status = "Approved", RowVersion = proposal.Value.RowVersion }, seed.LecturerUserId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await handler.ReviewAsync(proposal.Value.Id, new() { Status = "Approved" }, seed.LecturerUserId, SystemRoles.Admin)).Error.Code.Should().Be(ErrorCodes.ShowcaseConcurrencyConflict);
        var approved = await handler.ReviewAsync(proposal.Value.Id, new() { Status = "Approved", RowVersion = proposal.Value.RowVersion }, seed.LecturerUserId, SystemRoles.Admin);
        approved.IsSuccess.Should().BeTrue(); approved.Value.CreatedRuleId.Should().NotBeNull();
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(60);
        (await ShowcaseLanguageGuard.ValidateAsync(db, ["đần độn"], default)).Error.Code.Should().Be(ErrorCodes.ShowcaseLanguageInsult);
        (await handler.ReviewAsync(proposal.Value.Id, new() { Status = "Rejected", RowVersion = approved.Value.RowVersion }, seed.LecturerUserId, SystemRoles.Admin)).Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
        report.Status.Should().Be(ShowcaseReportStatus.Open); comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task ScopeValidationAndRejection_LeaveRulesAndReportsUnchanged()
    {
        await using var db = ShowcaseTestData.NewContext(); await db.Database.EnsureCreatedAsync();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var other = await ShowcaseTestData.AddUserAsync(db, "Other");
        var comment = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = other.Id, Content = "Bạn là đồ đần độn!" };
        var report = new ShowcaseReport { ShowcaseProjectId = seed.ShowcaseId, CommentId = comment.Id, ReporterUserId = other.Id, Reason = ShowcaseReportReason.Harassment };
        db.ShowcaseComments.Add(comment); db.ShowcaseReports.Add(report); await db.SaveChangesAsync();
        var handler = Handler(db);
        var request = new EHub.Contracts.Showcase.CreateShowcaseRuleProposalRequest { Phrase = "đần độn", Category = "Insult" };
        (await handler.CreateAsync(report.Id, request, Guid.Empty, SystemRoles.Admin)).Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
            (await handler.CreateAsync(report.Id, request, other.Id, role)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await handler.CreateAsync(report.Id, request, other.Id, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseReportNotFound);
        (await handler.ListAsync(null, 1, 20, seed.LecturerUserId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        (await handler.CreateAsync(report.Id, new() { Phrase = "dan don", Category = "Insult" }, seed.LecturerUserId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseRuleProposalInvalidPhrase);
        (await handler.CreateAsync(report.Id, new() { Phrase = "đần độn", Category = "Unknown" }, seed.LecturerUserId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        var proposal = (await handler.CreateAsync(report.Id, request, seed.LecturerUserId, SystemRoles.Lecturer)).Value;
        var rejected = await handler.ReviewAsync(proposal.Id, new() { Status = "Rejected", RowVersion = proposal.RowVersion }, seed.LecturerUserId, SystemRoles.Admin);
        rejected.Value.Status.Should().Be("Rejected");
        (await db.ShowcaseModerationRules.CountAsync()).Should().Be(59);
        report.Status = ShowcaseReportStatus.Dismissed; await db.SaveChangesAsync();
        (await handler.CreateAsync(report.Id, request, seed.LecturerUserId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
        (await db.ShowcaseModerationRuleProposals.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AdminConfirmation_ImmediatelyApprovesAndBlocksShortAccentedWords()
    {
        await using var db = ShowcaseTestData.NewContext(); await db.Database.EnsureCreatedAsync();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var admin = await ShowcaseTestData.AddUserAsync(db, "Admin");
        var comment = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = admin.Id, Content = "bò" };
        var report = new ShowcaseReport { ShowcaseProjectId = seed.ShowcaseId, CommentId = comment.Id, ReporterUserId = seed.OwnerUserId, Reason = ShowcaseReportReason.Harassment };
        db.ShowcaseComments.Add(comment); db.ShowcaseReports.Add(report); await db.SaveChangesAsync();
        var result = await Handler(db).CreateAsync(report.Id, new() { Phrase = "BÒ", Category = "Insult" }, admin.Id, SystemRoles.Admin);
        result.IsSuccess.Should().BeTrue(); result.Value.Status.Should().Be("Approved"); result.Value.CreatedRuleId.Should().NotBeNull();
        var audit = await db.ShowcaseModerationRuleProposals.SingleAsync();
        audit.ReviewedByUserId.Should().Be(admin.Id); audit.ReviewedAtUtc.Should().Be(Now);
        (await db.ShowcaseModerationRuleProposals.CountAsync(x => x.Status == ShowcaseRuleProposalStatus.Pending)).Should().Be(0);
        (await ShowcaseLanguageGuard.ValidateAsync(db, ["bò"], default)).Error.Code.Should().Be(ErrorCodes.ShowcaseLanguageInsult);
        foreach (var ordinary in new[] { "bo", "bỏ", "bóng" }) (await ShowcaseLanguageGuard.ValidateAsync(db, [ordinary], default)).IsSuccess.Should().BeTrue();
        report.Status.Should().Be(ShowcaseReportStatus.Open); comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task AdminConfirmation_ReusesAndApprovesTheLecturersPendingProposal()
    {
        await using var db = ShowcaseTestData.NewContext(); await db.Database.EnsureCreatedAsync();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var admin = await ShowcaseTestData.AddUserAsync(db, "Admin");
        var comment = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, AuthorUserId = admin.Id, Content = "đần độn" };
        var report = new ShowcaseReport { ShowcaseProjectId = seed.ShowcaseId, CommentId = comment.Id, ReporterUserId = seed.OwnerUserId, Reason = ShowcaseReportReason.Harassment };
        db.ShowcaseComments.Add(comment); db.ShowcaseReports.Add(report); await db.SaveChangesAsync();
        var handler = Handler(db);
        var pending = (await handler.CreateAsync(report.Id, new() { Phrase = "đần độn", Category = "Insult" }, seed.LecturerUserId, SystemRoles.Lecturer)).Value;
        var result = await handler.CreateAsync(report.Id, new() { Phrase = "ĐẦN ĐỘN", Category = "Defamation" }, admin.Id, SystemRoles.Admin);
        result.IsSuccess.Should().BeTrue(); result.Value.Id.Should().Be(pending.Id); result.Value.Status.Should().Be("Approved");
        var stored = await db.ShowcaseModerationRuleProposals.SingleAsync();
        stored.ProposedByUserId.Should().Be(seed.LecturerUserId); stored.ReviewedByUserId.Should().Be(admin.Id);
        stored.Category.Should().Be(ShowcaseViolationCategory.Defamation);
        (await db.ShowcaseModerationRules.CountAsync(x => x.Phrase == "đần độn")).Should().Be(1);
    }

    [Fact]
    public async Task CategoryCatalog_ContainsNamedGroups_AndReportValidationPreservesLegacyReasons()
    {
        await using var db = ShowcaseTestData.NewContext(); await db.Database.EnsureCreatedAsync();
        var catalog = (await new ShowcaseViolationCategoryQueryHandler(db).ListAsync()).Value;
        catalog.Select(x => x.Code).Should().BeEquivalentTo(Enum.GetNames<ShowcaseViolationCategory>());
        catalog.Should().OnlyContain(x => x.NameVi.Length > 0 && x.NameEn.Length > 0);
        var validator = new CreateShowcaseReportRequestValidator();
        (await validator.ValidateAsync(new CreateShowcaseReportRequest { Reason = "Harassment", Category = "Threat" })).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new CreateShowcaseReportRequest { Reason = "CopyrightViolation" })).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new CreateShowcaseReportRequest { Reason = "Other" })).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new CreateShowcaseReportRequest { Reason = "Harassment", Category = "Spam" })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new CreateShowcaseReportRequest { Reason = "Other", Category = "Unknown" })).IsValid.Should().BeFalse();
        var ruleValidator = new SaveShowcaseModerationRuleRequestValidator();
        (await ruleValidator.ValidateAsync(new SaveShowcaseModerationRuleRequest { Phrase = "bò", Category = "Insult", Reason = "Harassment", Description = "Short accented word" })).IsValid.Should().BeTrue();
        (await ruleValidator.ValidateAsync(new SaveShowcaseModerationRuleRequest { Phrase = "123", Category = "Insult", Reason = "Harassment", Description = "Numbers alone" })).IsValid.Should().BeFalse();
    }
}
