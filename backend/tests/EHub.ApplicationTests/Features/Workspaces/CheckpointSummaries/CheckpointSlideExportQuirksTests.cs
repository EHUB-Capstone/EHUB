using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

// Quirks seen in the text of slide decks exported to PDF by design tools, rebuilt here with invented wording. Slides lay
// a number or title out several times (a shadow or outline layer), split Vietnamese words at the tone mark, and put
// every cell of a table on its own line. The quick view and the figure comparison read this text, so a quirk that gets
// through shows up as a figure counted three times or a sentence nobody can read.
public sealed class CheckpointSlideExportQuirksTests
{
    [Theory]
    [InlineData("Check Point 2:Check Point 2:Check Point 2:", "Check Point 2:")]
    [InlineData("MARKETMARKETMARKET", "MARKET")]
    // A figure laid out three times: no letters in it, yet it is still one figure.
    [InlineData("60,4%60,4%60,4%", "60,4%")]
    [InlineData("84,3%84,3%84,3%", "84,3%")]
    [InlineData("3.23 / 53.23 / 53.23 / 5", "3.23 / 5")]
    // A single figure, a year and a range are not repeats.
    [InlineData("60,4%", "60,4%")]
    [InlineData("2020", "2020")]
    [InlineData("200-500k/tháng", "200-500k/tháng")]
    public void CollapseRepeat_TreatsAFigureLaidOutSeveralTimesAsOne(string input, string expected)
    {
        CheckpointLayoutText.CollapseRepeat(input).Should().Be(expected);
    }

    [Theory]
    // A capitalised word: the capital onset is split from its vowel as well as the ending.
    [InlineData("Nh ữ ng c ầ n chú ý", "Những cần chú ý")]
    [InlineData("Qu ả n lý còn th ủ công", "Quản lý còn thủ công")]
    [InlineData("CH Ủ Đ Ộ NG", "CHỦ ĐỘNG")]
    [InlineData("g ặ p", "gặp")]
    // Ordinary text is left alone, including a one-letter label in front of a lower-case word.
    [InlineData("Quản lý còn thủ công", "Quản lý còn thủ công")]
    [InlineData("Mất 3–5 giờ mỗi tuần", "Mất 3–5 giờ mỗi tuần")]
    [InlineData("Phương án B ở trên", "Phương án B ở trên")]
    public void JoinSplitSyllables_RepairsWordsSplitByAToneMarkGlyph(string input, string expected)
    {
        // Tidy collapses the double spaces a PDF leaves around the stray glyph before the repair runs.
        CheckpointLayoutText.Tidy(input, layoutText: true).Should().Be(expected);
    }

    [Fact]
    public void Tidy_KeepsOneCopyOfTheFiguresOfAStatCard()
    {
        // A stat card whose number, caption and icon were each drawn three times.
        const string card =
            "3.23 / 53.23 / 53.23 / 5\nMức độ hài lòng\n60,4%60,4%60,4%\nMức độ hài lòng với hệ thống\n" +
            "84,3%84,3%84,3%\nNhân sự mất >10 phút tra cứu một bộ hồ sơ";

        var tidy = CheckpointLayoutText.Tidy(card, layoutText: true);

        tidy.Split("60,4%").Length.Should().Be(2, "the figure appears once");
        tidy.Split("84,3%").Length.Should().Be(2, "the figure appears once");
        tidy.Split("3.23 / 5").Length.Should().Be(2, "the figure appears once");
    }

    [Fact]
    public void FigureComparison_DoesNotCountAFigureLaidOutThreeTimesAsThreeSources()
    {
        // The deck says 51 respondents; the report says 52. Tripled text in the deck must not hide or multiply the conflict.
        var deck = CheckpointLayoutText.Tidy("Quy mô mẫu hợp lệ: N = 51 nhân sự trong lĩnh vực Logistics.\n60,4%60,4%60,4%", layoutText: true);
        var report = CheckpointLayoutText.Tidy("52 doanh nghiệp tham gia khảo sát từ Forwarder, 3PL, Express và XNK.", layoutText: true);

        var conflicts = CheckpointCrossFileFigures.Compare(
        [
            ("deck.pdf", CheckpointCrossFileFigures.Collect(deck)),
            ("report.pdf", CheckpointCrossFileFigures.Collect(report))
        ]);

        conflicts.Should().ContainSingle().Which.Subject.Should().Be("Survey sample size");
    }
}
