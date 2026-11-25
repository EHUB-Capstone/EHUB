using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Application.Features.Showcase.Reports;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseAutoReportTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 1, 0, 0, DateTimeKind.Utc);
    private static IDateTimeProvider Clock() { var clock = Substitute.For<IDateTimeProvider>(); clock.UtcNow.Returns(Now); return clock; }
    private static ShowcaseModerationRule Rule(string phrase = "đồ ngu") => new() { Phrase = phrase, Description = "Possible insult", Reason = ShowcaseReportReason.Harassment };

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
    [InlineData("CDMM!!!")]
    [InlineData("CHÓ ĐẺ")]
    [InlineData("dit me")]
    [InlineData("tao sẽ giết mày")]
    [InlineData("giết cả nhà mày")]
    [InlineData("đập chết mày")]
    [InlineData("i will kill you")]
    [InlineData("đồ súc sinh")]
    [InlineData("đồ khốn nạn")]
    [InlineData("đồ lừa đảo")]
    [InlineData("lũ ăn cắp")]
    [InlineData("lũ da đen")]
    [InlineData("lũ da trắng")]
    [InlineData("chủng tộc hạ đẳng")]
    [InlineData("phim sex")]
    [InlineData("video sex")]
    [InlineData("ảnh khiêu dâm")]
    [InlineData("dịch vụ mại dâm")]
    [InlineData("ngu")]
    [InlineData("ngu ghê")]
    [InlineData("bạn ngu")]
    [InlineData("sao lại ngu thế")]
    [InlineData("NGU!")]
    [InlineData("  ngu  ")]
    [InlineData("Góp ý:\nngu ghê")]
    [InlineData("bạn\tngu\tghê")]
    [InlineData("Bạn (ngu), ghê!")]
    [InlineData("bạn nguuuu ghê")]
    [InlineData("óc bò")]
    [InlineData("bạn là óc bò")]
    [InlineData("súc sinh")]
    [InlineData("thật khốn nạn")]
    [InlineData("mất dạy quá")]
    [InlineData("thật vô học")]
    [InlineData("con cặc")]
    [InlineData("cái loz")]
    [InlineData("cai lồn")]
    [InlineData("cặc")]
    [InlineData("loz")]
    [InlineData("lồn")]
    [InlineData("CON CẶC!")]
    [InlineData("cái (loz)")]
    [InlineData("Bạn nói cái lồn gì")]
    [InlineData("cai\nlồn")]
    [InlineData("con\tcặc")]
    [InlineData("cặcccc")]
    [InlineData("lợn")]
    [InlineData("heo")]
    [InlineData("LỢN!")]
    [InlineData("đồ heo")]
    public async Task DefaultCatalog_RecognizesRequestedPhrases_InCommentsAndShowcases(string text)
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        var rules = await db.ShowcaseModerationRules.AsNoTracking().ToListAsync();
        rules.Should().HaveCount(59);
        ShowcaseContentFilter.Evaluate([text], rules, true).Should().NotBeEmpty();
        ShowcaseContentFilter.Evaluate([text], rules, false).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("classroom")]
    [InlineData("clmfoo")]
    [InlineData("lớn")]
    [InlineData("lộn")]
    [InlineData("nghèo")]
    [InlineData("dumedata")]
    [InlineData("chó đen")]
    [InlineData("Ốc lớn và đồ ngủ đẹp")]
    [InlineData("Ứng dụng giáo dục giới tính")]
    [InlineData("Tìm hiểu về phân biệt chủng tộc")]
    [InlineData("Dự án hỗ trợ người da đen")]
    [InlineData("Cảnh báo và phòng tránh lừa đảo")]
    [InlineData("Dự án này chưa khả thi")]
    [InlineData("ngủ")]
    [InlineData("ngủ ghê")]
    [InlineData("nguyên liệu")]
    [InlineData("Nguyễn nghiên cứu ngôn ngữ")]
    [InlineData("người dùng cần nghỉ ngơi")]
    [InlineData("nguồn nước sạch")]
    [InlineData("nguon nuoc sach")]
    [InlineData("ungulate")]
    [InlineData("ngughe")]
    [InlineData("nguyen")]
    [InlineData("Các bạn cùng tham gia")]
    [InlineData("cac ban cung tham gia")]
    [InlineData("Ứng dụng cho dự án lớn")]
    [InlineData("Tái chế vỏ lon")]
    [InlineData("cái lon")]
    [InlineData("cai lon")]
    [InlineData("lon")]
    [InlineData("cá lóc")]
    [InlineData("catalog")]
    [InlineData("lozenge")]
    public async Task ExpandedCatalog_PreservesWordBoundariesAndNonAbusiveVietnamese(string text)
    {
        await using var db = ShowcaseTestData.NewContext();
        await db.Database.EnsureCreatedAsync();
        var rules = await db.ShowcaseModerationRules.AsNoTracking().ToListAsync();
        ShowcaseContentFilter.Evaluate([text], rules, true).Should().BeEmpty();
    }

    [Theory]
    [InlineData("ĐỒ NGU")]
    [InlineData("do ngu")]
    [InlineData("đồ... nguuuuu")]
    [InlineData("d0 ngu")]
    [InlineData("Bạn là do ngu")]
    [InlineData("đ\u200bồ ngu")]
    public void Filter_RecognizesCaseAccentsSeparatorsRepeatsAndSomeObfuscation(string text) =>
        ShowcaseContentFilter.Evaluate([text], [Rule()], true).Should().ContainSingle();

    [Theory]
    [InlineData("Dự án này chưa khả thi")]
    [InlineData("đồ ngủ đẹp")]
    [InlineData("hello https://github.com/team/project")]
    [InlineData("đồ nguyen")]
    public void Filter_PreservesCriticismWordBoundariesAndNormalLinks(string text) =>
        ShowcaseContentFilter.Evaluate([text], [Rule()], true).Should().BeEmpty();

    [Fact]
    public void Filter_HonorsDisabledRules_AndDoesNotJoinSeparateFields()
    {
        var rule = Rule(); rule.IsEnabled = false;
        ShowcaseContentFilter.Evaluate(["đồ ngu"], [rule], true).Should().BeEmpty();
        rule.IsEnabled = true;
        ShowcaseContentFilter.Evaluate(["đồ", "ngu"], [rule], false).Should().BeEmpty();
    }

    [Fact]
    public void ManyLinks_AreACommentSignal_NotAShowcaseViolation()
    {
        const string links = "https://a.test https://b.test https://c.test https://d.test";
        ShowcaseContentFilter.Evaluate([links], [], true).Should().ContainSingle().Which.Reason.Should().Be(ShowcaseReportReason.Spam);
        ShowcaseContentFilter.Evaluate([links], [], false).Should().BeEmpty();
    }

    [Fact]
    public void DuplicateNormalization_PreservesVietnameseWordMeanings() =>
        ShowcaseContentFilter.NormalizeComment("Bộ đồ ngủ đẹp").Should().NotBe(ShowcaseContentFilter.NormalizeComment("Bộ đồ ngu đẹp"));

    [Fact]
    public async Task OnlyUserReports_AppearInTheTeacherQueue_AndLegacySystemFlagsCannotBeResolved()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        db.ShowcaseModerationRules.Add(Rule());
        (await db.ShowcaseProjects.SingleAsync()).Highlights = "đồ ngu";
        await db.SaveChangesAsync();
        await new ShowcaseAutoReportScanner(db, Clock()).ScanPageAsync(seed.ShowcaseId);
        var reporter = await ShowcaseTestData.AddUserAsync(db, "Reporter");
        var reports = new ShowcaseReportHandler(db, Clock(), new ShowcaseModerationHandler(db, Clock()),
            new CreateShowcaseReportRequestValidator(), new ResolveShowcaseReportRequestValidator());
        (await reports.CreateAsync(seed.ShowcaseId, new() { Reason = "Harassment" }, reporter.Id)).IsSuccess.Should().BeTrue();
        var queue = await reports.ListAsync("Open", 1, 20, seed.LecturerUserId, SystemRoles.Lecturer);
        var item = queue.Value.Items.Should().ContainSingle().Which;
        item.ReportCount.Should().Be(1); item.UserReportCount.Should().Be(1); item.SystemReportCount.Should().Be(0);
        item.Sources.Should().BeEquivalentTo(new[] { "User" });
        var system = await db.ShowcaseReports.SingleAsync(x => x.Source == ShowcaseReportSource.System);
        (await reports.ResolveAsync(system.Id, new() { Status = "ActionTaken" }, seed.LecturerUserId, SystemRoles.Lecturer)).IsFailure.Should().BeTrue();
        (await reports.ResolveAsync(item.Id, new() { Status = "Dismissed" }, seed.LecturerUserId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await db.ShowcaseReports.SingleAsync(x => x.Source == ShowcaseReportSource.User)).Status.Should().Be(ShowcaseReportStatus.Dismissed);
        (await db.ShowcaseReports.SingleAsync(x => x.Source == ShowcaseReportSource.System)).Status.Should().Be(ShowcaseReportStatus.Open);
    }

    [Theory]
    [InlineData(ShowcaseStatus.Published)]
    [InlineData(ShowcaseStatus.PendingReview)]
    public async Task Scanner_FlagsOnceWithoutChangingVisibility_AndNotifiesReviewers(ShowcaseStatus status)
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, status);
        db.ShowcaseModerationRules.Add(Rule());
        var page = await db.ShowcaseProjects.SingleAsync(); page.Highlights = "đồ ngu";
        await db.SaveChangesAsync();
        var scanner = new ShowcaseAutoReportScanner(db, Clock());
        await scanner.ScanPageAsync(seed.ShowcaseId); await scanner.ScanPageAsync(seed.ShowcaseId);
        var report = (await db.ShowcaseReports.ToListAsync()).Should().ContainSingle().Subject;
        report.Source.Should().Be(ShowcaseReportSource.System); report.ReporterUserId.Should().BeNull();
        report.ContentFingerprint.Should().HaveLength(64); report.DetectionKey.Should().HaveLength(64);
        page.Status.Should().Be(status); report.CreatedAt.Should().Be(Now);
        (await db.ShowcaseModerationScans.SingleAsync()).ScannedAt.Should().Be(Now);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Scanner_CatchesProjectProfileEdits_AndSupersedesOldFlagAfterCorrection()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        db.ShowcaseModerationRules.Add(Rule()); await db.SaveChangesAsync();
        var scanner = new ShowcaseAutoReportScanner(db, Clock());
        await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseReports.CountAsync()).Should().Be(0);
        var project = await db.Projects.SingleAsync(); project.Description = "đồ ngu"; await db.SaveChangesAsync();
        await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseReports.SingleAsync()).Status.Should().Be(ShowcaseReportStatus.Open);
        project.Description = "Constructive product description"; await db.SaveChangesAsync();
        await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseReports.SingleAsync()).Status.Should().Be(ShowcaseReportStatus.Dismissed);
    }

    [Fact]
    public async Task HistoryDeletion_DoesNotRecreateUnchangedFlag_ButNewContentCanBeFlagged()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        db.ShowcaseModerationRules.Add(Rule()); var page = await db.ShowcaseProjects.SingleAsync(); page.Highlights = "đồ ngu";
        await db.SaveChangesAsync(); var scanner = new ShowcaseAutoReportScanner(db, Clock());
        await scanner.ScanPageAsync(seed.ShowcaseId);
        var report = await db.ShowcaseReports.SingleAsync(); report.Status = ShowcaseReportStatus.Dismissed; await db.SaveChangesAsync();
        db.ShowcaseReports.Remove(report); await db.SaveChangesAsync(); await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseReports.CountAsync()).Should().Be(0);
        page.Highlights = "đồ ngu!"; await db.SaveChangesAsync(); await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Scanner_CoversReplies_AndSkipsDeletedCommentsAndUnpublishedPages()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        db.ShowcaseModerationRules.Add(Rule());
        var root = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, Content = "Normal root" };
        var reply = new ShowcaseComment { ShowcaseProjectId = seed.ShowcaseId, ParentCommentId = root.Id, Content = "đồ ngu" };
        db.ShowcaseComments.AddRange(root, reply); await db.SaveChangesAsync();
        var scanner = new ShowcaseAutoReportScanner(db, Clock());
        await scanner.ScanCommentAsync(reply.Id);
        (await db.ShowcaseReports.SingleAsync()).CommentId.Should().Be(reply.Id);
        (await db.ShowcaseProjects.SingleAsync()).Status = ShowcaseStatus.Hidden; await db.SaveChangesAsync();
        await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseModerationScans.CountAsync()).Should().Be(1);
        reply.IsDeleted = true; await db.SaveChangesAsync(); await scanner.ScanCommentAsync(reply.Id);
        (await db.ShowcaseReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RuleChanges_ReevaluateContent_AndAdminPermissionIsRequired()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var page = await db.ShowcaseProjects.SingleAsync(); page.Highlights = "đồ ngu"; await db.SaveChangesAsync();
        var scanner = new ShowcaseAutoReportScanner(db, Clock()); await scanner.ScanPageAsync(seed.ShowcaseId);
        var handler = new ShowcaseModerationRuleHandler(db, new SaveShowcaseModerationRuleRequestValidator());
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor, SystemRoles.Lecturer })
        {
            (await handler.ListAsync(role, default)).IsFailure.Should().BeTrue();
            (await handler.SaveAsync(null, new() { Phrase = "đồ ngu", Description = "Test" }, role, default)).IsFailure.Should().BeTrue();
        }
        var rule = await handler.SaveAsync(null, new() { Phrase = "đồ ngu", Description = "Test" }, SystemRoles.Admin, default);
        rule.IsSuccess.Should().BeTrue();
        await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseReports.CountAsync()).Should().Be(1);
        (await handler.SaveAsync(rule.Value.Id, new() { Phrase = "đồ ngu", Description = "Test", IsEnabled = false, RowVersion = rule.Value.RowVersion }, SystemRoles.Admin, default)).IsSuccess.Should().BeTrue();
        await scanner.ScanPageAsync(seed.ShowcaseId);
        (await db.ShowcaseReports.SingleAsync()).Status.Should().Be(ShowcaseReportStatus.Dismissed);
    }
}
