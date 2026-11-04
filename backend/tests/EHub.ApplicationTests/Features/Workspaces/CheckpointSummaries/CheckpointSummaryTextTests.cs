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

    [Fact]
    public void Clean_RemovesCharactersThatPostgresJsonbRefusesButKeepsTextAndLineBreaks()
    {
        // PDF fonts can yield NUL characters; "\u0000" inside a jsonb value makes PostgreSQL reject the whole row.
        var dirty = "Kế hoạch\0 gọi vốn\u0001\n\tNhóm 😀 xin\ud800 chào\udc00";

        var clean = CheckpointSummaryText.Clean(dirty);

        clean.Should().Be("Kế hoạch gọi vốn\n\tNhóm 😀 xin chào");
        CheckpointSummaryText.Clean(null).Should().BeEmpty();
        System.Text.Json.JsonSerializer.Serialize(clean).Should().NotContain("\\u0000").And.NotContain("\\ud800");
    }

    [Fact]
    public void Clip_TrimsAndMarksLongText()
    {
        CheckpointSummaryText.Clip("  ngắn  ", 10).Should().Be("ngắn");
        CheckpointSummaryText.Clip(new string('a', 20), 10).Should().Be(new string('a', 10) + "…");
        CheckpointSummaryText.Clip(null, 10).Should().BeEmpty();
    }
}
