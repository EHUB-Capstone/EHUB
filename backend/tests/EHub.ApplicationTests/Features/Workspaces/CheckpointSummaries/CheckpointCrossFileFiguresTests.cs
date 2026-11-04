using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>Two files of one checkpoint that state the same survey differently (EXE101 Checkpoint 2: N=51 and 52, 84,3% and 82,7%).</summary>
public sealed class CheckpointCrossFileFiguresTests
{
    private const string Slides =
        "Quy mô mẫu hợp lệ: N = 51 nhân sự trong lĩnh vực Logistics.\n" +
        "Lãng phí vô hình: 84.3% nhân sự tốn hơn 10 phút chỉ để tìm lại một bộ chứng từ cũ.\n" +
        "FreightForwarder chiếm 35.3% số người tham gia khảo sát.";

    private const string Report =
        "52 Doanh nghiệp tham gia khảo sát từ Forwarder, 3PL, Express và XNK.\n" +
        "82,7% doanh nghiệp mất từ 11 đến hơn 30 phút để tìm bộ chứng từ cũ, rủi ro lớn nhất là thiếu sót chứng từ.\n" +
        "98% doanh nghiệp sẵn sàng trải nghiệm bản dùng thử sớm.";

    private static IReadOnlyList<(string, IReadOnlyCollection<string>)> Files(params (string Name, string Text)[] files) =>
        files.Select(file => (file.Name, (IReadOnlyCollection<string>)CheckpointCrossFileFigures.Collect(file.Text))).ToArray();

    [Fact]
    public void Collect_KeepsSentencesWithASampleSizeOrAPercentage_SampleSizesFirst()
    {
        var figures = CheckpointCrossFileFigures.Collect(
            "Nhóm họp vào thứ hai hàng tuần tại phòng lab của trường.\n" + Slides);

        figures.Should().HaveCount(3);
        figures[0].Should().StartWith("Quy mô mẫu");
        figures.Should().NotContain(sentence => sentence.StartsWith("Nhóm họp"));
    }

    [Fact]
    public void Compare_UsesThePercentageNextToTheWordsNotAnotherOneOnTheSameChartLine()
    {
        const string slide = "- Mất nhiều thời gian tra cứu (84,3% nhân sự mất trên 10 phút).";
        const string chart = "52 Doanh nghiệp tham gia 98% Doanh nghiệp khảo sát 82,7% Mất nhiều thời gian tra cứu 4,35/5.0 Điểm rủi ro cao nhất";

        var conflicts = CheckpointCrossFileFigures.Compare(Files(("slide.pdf", slide), ("chart.pdf", chart)));

        var conflict = conflicts.Single(item => item.Subject.StartsWith("Similar statement"));
        conflict.Values.Select(value => value.Value).Should().BeEquivalentTo("84,3%", "82,7%");
    }

    [Fact]
    public void Compare_ReportsTheSamePairOfValuesBetweenTwoFilesOnce()
    {
        const string slide =
            "Lãng phí vô hình: 84,3% nhân sự tốn hơn 10 phút chỉ để tìm lại một bộ chứng từ cũ.\n" +
            "- Mất nhiều thời gian tra cứu (84,3% nhân sự mất trên 10 phút).";
        const string report =
            "82,7% doanh nghiệp mất từ 11 đến hơn 30 phút để tìm bộ chứng từ cũ, rủi ro lớn nhất là thiếu sót.\n" +
            "82,7% Mất nhiều thời gian tra cứu 4,35/5.0 Điểm rủi ro cao nhất";

        CheckpointCrossFileFigures.Compare(Files(("slide.pdf", slide), ("report.pdf", report)))
            .Should().ContainSingle(conflict => conflict.Subject.StartsWith("Similar statement"));
    }

    [Fact]
    public void Compare_FlagsADifferentSampleSizeAndTheSameStatementWithADifferentPercentage()
    {
        var conflicts = CheckpointCrossFileFigures.Compare(Files(("SLIDE TEAM 3.pdf", Slides), ("BẢNG KHẢO SÁT.pdf", Report)));

        var sample = conflicts.Single(conflict => conflict.Subject == "Survey sample size");
        sample.Values.Select(value => (value.Value, value.Source))
            .Should().BeEquivalentTo([("51", "SLIDE TEAM 3.pdf"), ("52", "BẢNG KHẢO SÁT.pdf")]);

        var percent = conflicts.Single(conflict => conflict.Subject.StartsWith("Similar statement"));
        percent.Subject.Should().Contain("bộ chứng từ cũ");
        percent.Values.Select(value => value.Value).Should().BeEquivalentTo("84.3%", "82,7%");
        percent.Values.Should().OnlyContain(value => value.Quote.Length > 0 && value.Source != null);
    }

    [Fact]
    public void Compare_IgnoresEqualFiguresAndUnrelatedPercentages()
    {
        var same = "Quy mô mẫu: N = 51 nhân sự.\nLãng phí vô hình: 84,3% nhân sự tốn hơn 10 phút để tìm lại một bộ chứng từ cũ.";
        CheckpointCrossFileFigures.Compare(Files(("a.pdf", same), ("b.pdf", same))).Should().BeEmpty();

        // Different percentages about different things are not a conflict.
        CheckpointCrossFileFigures.Compare(Files(("a.pdf", Slides),
            ("b.pdf", "98% doanh nghiệp sẵn sàng trải nghiệm bản dùng thử sớm trong quý đầu tiên của năm sau."))).Should().BeEmpty();
    }

    [Fact]
    public void Compare_NeedsAtLeastTwoFiles()
    {
        CheckpointCrossFileFigures.Compare(Files(("only.pdf", Slides))).Should().BeEmpty();
        CheckpointCrossFileFigures.Compare([]).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Quy mô mẫu hợp lệ: N = 51 nhân sự.", 51)]
    [InlineData("100+ người trả lời, 83% là người trực tiếp quản lý nhân viên.", 100)]
    [InlineData("52 Doanh nghiệp tham gia khảo sát.", 52)]
    [InlineData("Nhà hàng fine-dining có 30–80 nhân sự.", null)]
    [InlineData("98% Doanh nghiệp khảo sát 82,7% mất nhiều thời gian.", null)]
    public void SampleSize_ReadsRespondentsOnlyInASentenceAboutASurvey(string sentence, int? expected)
    {
        CheckpointCrossFileFigures.SampleSize(sentence).Should().Be(expected);
    }

    [Fact]
    public void Preview_CarriesTheFiguresSoTheyCanBeComparedLater()
    {
        var preview = CheckpointSummaryEvidence.Preview(Slides, [], CheckpointSummaryTopics.None);

        preview.Figures.Should().NotBeEmpty();
    }
}
