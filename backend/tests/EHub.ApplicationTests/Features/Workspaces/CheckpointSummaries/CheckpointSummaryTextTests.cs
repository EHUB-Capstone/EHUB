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
    public void Select_KeepsInformativeSentencesInOrderAndDropsRepeatedLogs()
    {
        var log = string.Join('\n', Enumerable.Range(1, 200).Select(i =>
            $"Phụ lục {i}: Đội vận hành kiểm tra xe và ghi nhận phản hồi về đèn xe buổi tối của người dùng trong ký túc xá."));
        var text = "1. Tóm tắt\nGreenRide cho thuê xe đạp điện trong ký túc xá.\n2. Kế hoạch gọi vốn\n" +
                   "Nhóm kêu gọi 500 triệu đồng vốn hạt giống, đổi lấy 12% cổ phần.\n" + log;

        var selected = CheckpointSummaryText.Select(text, ["Kế hoạch gọi vốn"], 350);

        selected.Length.Should().BeLessThanOrEqualTo(350);
        selected.Should().Contain("500 triệu đồng vốn hạt giống").And.Contain("2. Kế hoạch gọi vốn");
        selected.IndexOf("1. Tóm tắt", StringComparison.Ordinal).Should().BeLessThan(selected.IndexOf("500 triệu", StringComparison.Ordinal));
        // 200 near-identical weekly logs never push the informative sentences out of a small budget.
        selected.Split('\n').Count(line => line.StartsWith("Phụ lục", StringComparison.Ordinal)).Should().BeLessThanOrEqualTo(2);
        CheckpointSummaryText.Select("Ngắn.", ["Kế hoạch"], 1_000).Should().Be("Ngắn.");
    }

    [Theory]
    [InlineData("Kế hoạch gọi vốn", "Nhóm kêu gọi 500 triệu đồng vốn hạt giống, gọi vốn từ quỹ trường.")]
    [InlineData("Phân tích đối thủ cạnh tranh", "Đối thủ trực tiếp là túi vải in sẵn trên Shopee.")]
    // A matching heading points to the first sentence under it.
    [InlineData("Dự báo tài chính", "Năm đầu dự kiến doanh thu 1,8 tỷ đồng và lợi nhuận 450 triệu đồng.")]
    public void BestEvidence_ReturnsASentenceThatNamesTheRequirement(string title, string expected)
    {
        string[] sentences =
        [
            "Giải pháp là túi vải bạt tái chế giá 45.000 đồng.",
            "Dự báo tài chính",
            "Năm đầu dự kiến doanh thu 1,8 tỷ đồng và lợi nhuận 450 triệu đồng.",
            "Đối thủ trực tiếp là túi vải in sẵn trên Shopee.",
            "Nhóm kêu gọi 500 triệu đồng vốn hạt giống, gọi vốn từ quỹ trường."
        ];

        CheckpointSummaryText.BestEvidence(title, sentences).Should().Be(expected);
    }

    [Theory]
    [InlineData("Kế hoạch phân công nhóm")]
    [InlineData("MVP")]
    [InlineData("Kết quả khảo sát hoặc phỏng vấn")]
    public void BestEvidence_IsNullWhenNoSentenceNamesTheRequirement(string title)
    {
        CheckpointSummaryText.BestEvidence(title, ["Giải pháp là túi vải bạt tái chế giá 45.000 đồng.", "Hoặc của các bên khác."])
            .Should().BeNull();
    }

    [Fact]
    public void Clip_TrimsAndMarksLongText()
    {
        CheckpointSummaryText.Clip("  ngắn  ", 10).Should().Be("ngắn");
        CheckpointSummaryText.Clip(new string('a', 20), 10).Should().Be(new string('a', 10) + "…");
        CheckpointSummaryText.Clip(null, 10).Should().BeEmpty();
    }
}
