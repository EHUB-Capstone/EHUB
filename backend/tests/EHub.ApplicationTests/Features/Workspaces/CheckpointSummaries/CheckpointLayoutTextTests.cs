using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointLayoutTextTests
{
    [Theory]
    // Slide PDFs place the tone mark as its own glyph, which arrives as a space inside the word.
    [InlineData("NHÓM ĐÃ TIÊ P THU THEO ĐÂ Y ĐỦ GÓP Ý", "NHÓM ĐÃ TIÊP THU THEO ĐÂY ĐỦ GÓP Ý")]
    [InlineData("Sau khi upload hế t đề tài, hệ thố ng tự động", "Sau khi upload hết đề tài, hệ thống tự động")]
    [InlineData("KHO LƯU TRỮ NĂ M TRONG SCOPE", "KHO LƯU TRỮ NĂM TRONG SCOPE")]
    // Real word boundaries stay: a whole next word, a lower-case vowel word, or a plain ASCII letter before the space.
    [InlineData("SỬ DỤNG AI HÔ TRỢ CẢNH BÁO", "SỬ DỤNG AI HÔ TRỢ CẢNH BÁO")]
    [InlineData("Phòng khám chú ý y tế", "Phòng khám chú ý y tế")]
    [InlineData("Plan n and p", "Plan n and p")]
    public void JoinSplitSyllables_RepairsWordsBrokenAtTheToneMark(string input, string expected)
    {
        CheckpointLayoutText.JoinSplitSyllables(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("2.PROJECT2.PROJECT", "2.PROJECT")]
    [InlineData("MANAGEMENTMANAGEMENT", "MANAGEMENT")]
    [InlineData("6. DEMO6. DEMO6. DEMO6. DEMO", "6. DEMO")]
    [InlineData("2020", "2020")]
    [InlineData("Doanh thu 1,8 tỷ", "Doanh thu 1,8 tỷ")]
    public void CollapseRepeat_RemovesTitlesRepeatedByAnimatedSlides(string input, string expected)
    {
        CheckpointLayoutText.CollapseRepeat(input).Should().Be(expected);
    }

    [Fact]
    public void Tidy_JoinsBrokenSentencesAndDropsRepeatedTitlesInSlideText()
    {
        const string slides =
            "DATABASE DESIGN\nAcademic Management\nDATABASE DESIGN\nEvaluation and Analytics\n" +
            "NHÓM ĐÃ TIẾP THU VÀ CẬP NHẬT REPORT 3 THEO ĐẦY ĐỦ GÓP Ý\nCỦA CÔ: CHUẨN HÓA HÌNH VẼ.\n" +
            "Bỏ chức năng đăng ký. Dùng actor Guest, và các\nactor kế thừa từ User có thể login.\n" +
            "1. PENDING: NGHIÊN CỨU SAU KHI\nHOÀN THÀNH PHẠM VI.\n2. IN PROGRESS: CẢNH BÁO TRÙNG ĐỀ TÀI.\n" +
            "Data present: 67 migrations, 147 users, 17\nsubmissions";

        var lines = CheckpointLayoutText.Tidy(slides, layoutText: true).Split('\n');

        lines.Should().Equal(
            "DATABASE DESIGN",
            "Academic Management",
            "Evaluation and Analytics",
            "NHÓM ĐÃ TIẾP THU VÀ CẬP NHẬT REPORT 3 THEO ĐẦY ĐỦ GÓP Ý CỦA CÔ: CHUẨN HÓA HÌNH VẼ.",
            "Bỏ chức năng đăng ký. Dùng actor Guest, và các actor kế thừa từ User có thể login.",
            "1. PENDING: NGHIÊN CỨU SAU KHI HOÀN THÀNH PHẠM VI.",
            "2. IN PROGRESS: CẢNH BÁO TRÙNG ĐỀ TÀI.",
            "Data present: 67 migrations, 147 users, 17 submissions");
    }

    [Fact]
    public void Tidy_KeepsRepeatedLinesOfWordDocuments()
    {
        // In a Word table the same short value ("Có") can be real content in several rows.
        CheckpointLayoutText.Tidy("Có\nKhông\nCó", layoutText: false).Should().Be("Có\nKhông\nCó");
    }

    [Fact]
    public void Grounding_DropsInventedDetailsButKeepsPointsBackedByTheFile()
    {
        const string source =
            "AUTOMATED DATABASE BACKUP\nRuns automatically every day at 02:00\nTwo copies: VPS (14 days) and private Cloudflare R2\n" +
            "Integrity checked with SHA-256 checksum\nDATABASE DESIGN\nAcademic Management\nProject Repository and Progress Tracking\n" +
            "Evaluation and Analytics\nMentor Matching and Support\nStartup Data Bank and Incubation\n" +
            "Bỏ chức năng “User đăng ký tài khoản”. Dùng actor Guest.\nMục 3.2 của Report 3 không đúng template.";
        var grounding = new CheckpointSummaryGrounding(source);

        // Vietnamese summary of English slides: backed by its distinctive terms.
        grounding.Supports("Hệ thống sao lưu hằng ngày lúc 02:00, hai bản trên VPS và Cloudflare R2, kiểm tra SHA-256.").Should().BeTrue();
        grounding.Supports("Nhóm bỏ chức năng User đăng ký tài khoản và dùng actor Guest.").Should().BeTrue();
        // A count the file never gives, and wording the file never uses.
        grounding.Supports("Cơ sở dữ liệu có 5 module được tối ưu hóa hiệu suất truy xuất.").Should().BeFalse();
        grounding.Supports("Quy trình quản lý dự án từ đầu tư đến hoàn thành và mô phỏng cho doanh nhân.").Should().BeFalse();
    }

    [Fact]
    public void GroundedOverview_KeepsOnlySupportedSentencesIncludingTheOpeningClaim()
    {
        var grounding = new CheckpointSummaryGrounding("Mục 3.2 của Report 3 không đúng template. Dùng actor Guest.");

        grounding.GroundedOverview("Tài liệu là slide review của nhóm. Mục 3.2 của Report 3 chưa đúng template. Nhóm gọi vốn 5 tỷ từ quỹ đầu tư.")
            .Should().Be("Mục 3.2 của Report 3 chưa đúng template.");
    }

    [Fact]
    public void Highlights_PreferReadableSentencesOverCapitalBannersAndMemberLists()
    {
        const string text =
            "MR. NGUYEN XUAN LONG BUI MANH THINH - [MSSV] - TEAM LEADER MRS. NGUYEN THI TU SUONG - [MSSV] OUR TEAM 2026\n" +
            "ENTREPRENEURSHIP HUB MANAGEMENT AND STARTUP INCUBATION SUPPORT PLATFORM 2026\n" +
            "Nội dung giới thiệu chung về nền tảng hỗ trợ khởi nghiệp của nhóm.\n" +
            "Mục 3.2 của Report 3 không đúng template và cần chỉnh lại theo mẫu.\n" +
            "Restore completed without errors in 5.34 seconds with 147 users and 17 submissions.\n" +
            "Bỏ chức năng User đăng ký tài khoản, dùng actor Guest cho người chưa đăng nhập.";

        var highlights = CheckpointSummaryText.Highlights(text, ["Kế hoạch gọi vốn"], 3);

        highlights.Should().NotContain(item => item.Contains("NGUYEN") || item.Contains("ENTREPRENEURSHIP"));
        highlights.Should().Contain("Restore completed without errors in 5.34 seconds with 147 users and 17 submissions.");
    }

    [Fact]
    public void Prompt_WarnsAboutImagesOnlyForFilesWithLittleText()
    {
        var slides = CheckpointSummaryPrompt.User(2, "Review", [], "review.pdf", "CONTENTS", excerpt: false, littleText: true);
        var report = CheckpointSummaryPrompt.User(2, "Review", [], "report.docx", "Nội dung", excerpt: false);

        CheckpointSummaryPrompt.Version.Should().Be("v13");
        slides.Should().Contain("phần lớn là hình ảnh").And.Contain("Mục lục");
        report.Should().NotContain("phần lớn là hình ảnh").And.Contain("Mục lục");
    }
}
