using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointSummaryTextTests
{
    [Fact]
    public void Redact_RemovesEmailsPhoneNumbersAndStudentCodesButKeepsOrdinaryWords()
    {
        var redacted = CheckpointSummaryText.Redact(
            "Liên hệ truongnhom.demo@example.com, điện thoại 0901 234 567 hoặc +84912345678, MSSV SE190001. " +
            "Ứng dụng chạy trên điện thoại và có 1.200 người dùng.");

        redacted.Should().NotContain("example.com").And.NotContain("0901").And.NotContain("912345678").And.NotContain("SE190001");
        redacted.Should().Contain("[email]").And.Contain("[số điện thoại]").And.Contain("[MSSV]");
        redacted.Should().Contain("chạy trên điện thoại").And.Contain("1.200 người dùng");
    }

    [Fact]
    public void Chunks_KeepEveryCharacterAndStayWithinTheSize()
    {
        var text = string.Join('\n', Enumerable.Range(1, 400).Select(i => $"Dòng {i}: nhóm ghi nhận phản hồi số {i} của người dùng."))
            + "\n" + new string('x', 9_000);

        var chunks = CheckpointSummaryText.Chunks(text, 2_000).ToArray();

        chunks.Should().HaveCountGreaterThan(5);
        chunks.Should().OnlyContain(chunk => chunk.Length <= 2_001);
        string.Concat(chunks).Replace("\n", string.Empty).Should().Be(text.Replace("\n", string.Empty));
    }

    [Fact]
    public void IsGrounded_AcceptsExactAndLightlyEditedQuotesButRejectsInventedOnes()
    {
        const string source = "Trong 4 tuần bán thử, FitBox nhận 320 đơn từ 96 khách hàng. Tỷ lệ khách quay lại sau tuần đầu là 38%.";
        var trigrams = CheckpointSummaryText.Trigrams(CheckpointSummaryText.Normalize(source));

        CheckpointSummaryText.IsGrounded("Trong 4 tuần bán thử, FitBox nhận 320 đơn từ 96 khách hàng.", trigrams).Should().BeTrue();
        // Models often join sentences and drop punctuation.
        CheckpointSummaryText.IsGrounded("FitBox nhận 320 đơn từ 96 khách hàng; tỷ lệ khách quay lại sau tuần đầu là 38%", trigrams).Should().BeTrue();
        CheckpointSummaryText.IsGrounded("Nhóm đã ký hợp đồng với 5 công ty và gọi vốn 2 tỷ đồng.", trigrams).Should().BeFalse();
        CheckpointSummaryText.IsGrounded("", trigrams).Should().BeFalse();
    }

    [Theory]
    [InlineData("Kế hoạch gọi vốn", "Nhóm kêu gọi 500 triệu đồng vốn hạt giống, gọi vốn từ quỹ trường.", true)]
    [InlineData("Phân tích đối thủ cạnh tranh", "Đối thủ trực tiếp là túi vải in sẵn trên Shopee.", true)]
    [InlineData("Kế hoạch gọi vốn", "Giải pháp là túi vải bạt tái chế giá 45.000 đồng.", false)]
    [InlineData("Kết quả khảo sát hoặc phỏng vấn", "Hoặc của các bên khác.", false)]
    [InlineData("MVP", "MVP là landing page.", false)]
    public void IsRelevant_RequiresATwoWordPhraseFromTheRequirement(string title, string quote, bool expected)
    {
        CheckpointSummaryText.IsRelevant(title, quote).Should().Be(expected);
    }

    [Fact]
    public void Clip_TrimsAndMarksLongText()
    {
        CheckpointSummaryText.Clip("  ngắn  ", 10).Should().Be("ngắn");
        CheckpointSummaryText.Clip(new string('a', 20), 10).Should().Be(new string('a', 10) + "…");
        CheckpointSummaryText.Clip(null, 10).Should().BeEmpty();
    }
}
