using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointSummaryConsistencyTests
{
    private const string ReviewSlide =
        "HaiVA\n" +
        "1.Thêm chức năng trao đổi giữa mentor doanh nghiệp và giảng viên đứng lớp.\n" +
        "2.Sau khi upload hết đề tài trong kỳ, hệ thống tự động phát hiện các nhóm trùng ý tưởng.\n" +
        "3.Tổng hợp kho lưu trữ ý tưởng để các nhóm kỳ sau kế thừa.\n" +
        "1. PENDING: NHÓM XIN GHI NHẬN VÀ SẼ NGHIÊN CỨU SAU KHI HOÀN THÀNH PHẠM VI (SCOPE) CỦA DỰ ÁN.\n" +
        "2. IN PROGRESS: SỬ DỤNG AI HỖ TRỢ CẢNH BÁO TRÙNG ĐỀ TÀI ĐỂ GIẢNG VIÊN QUYẾT ĐỊNH.\n" +
        "3. DONE: KHO LƯU TRỮ PROJECT NẰM TRONG SCOPE VÀ ĐÃ ĐƯỢC XÂY DỰNG.";

    [Fact]
    public void StatusLines_PairEachStatusWithTheRequestItAnswersAndUseSentenceCase()
    {
        var items = CheckpointStatusLines.Find(ReviewSlide);

        items.Select(item => item.Status).Should().Equal("Pending", "InProgress", "Done");
        items[0].Request.Should().Be("Thêm chức năng trao đổi giữa mentor doanh nghiệp và giảng viên đứng lớp.");
        items[0].Text.Should().StartWith("Nhóm xin ghi nhận").And.NotContain("NHÓM");
        items[1].Request.Should().StartWith("Sau khi upload hết đề tài");
        items[2].Text.Should().Be("Kho lưu trữ project nằm trong scope và đã được xây dựng.");
        // Short ASCII words such as AI keep their capitals.
        items[1].Text.Should().Contain("AI");
    }

    [Theory]
    [InlineData("NHÓM ĐÃ TIẾP THU VÀ CẬP NHẬT REPORT 3 THEO ĐẦY ĐỦ GÓP Ý", "Nhóm đã tiếp thu và cập nhật report 3 theo đầy đủ góp ý")]
    [InlineData("SỬ DỤNG AI HỖ TRỢ CẢNH BÁO TRÙNG ĐỀ TÀI", "Sử dụng AI hỗ trợ cảnh báo trùng đề tài")]
    [InlineData("Beta Launch — Public beta preparation", "Beta Launch — Public beta preparation")]
    [InlineData("Restore completed without errors", "Restore completed without errors")]
    public void SentenceCase_OnlyChangesTextWrittenInCapitals(string input, string expected)
    {
        CheckpointStatusLines.SentenceCase(input).Should().Be(expected);
    }

    [Fact]
    public void Contradicts_FindsASentenceCallingPendingOrInProgressWorkFinished()
    {
        var items = CheckpointStatusLines.Find(ReviewSlide);

        CheckpointStatusLines.Contradicts(
            "Đã xác nhận thêm chức năng trao đổi trực tiếp giữa mentor doanh nghiệp và giảng viên đứng lớp.", items).Should().BeTrue();
        CheckpointStatusLines.Contradicts(
            "Đã triển khai AI hỗ trợ cảnh báo trùng đề tài để giảng viên quyết định.", items).Should().BeTrue();
        // Finished work, and sentences without a completion word, are left alone.
        CheckpointStatusLines.Contradicts("Đã xây dựng kho lưu trữ project nằm trong scope.", items).Should().BeFalse();
        // Sharing a few common words with another item's long request ("kỳ", "tự động", "ý tưởng") is not enough.
        CheckpointStatusLines.Contradicts(
            "Hệ thống đã hoàn thành việc tổng hợp kho lưu trữ ý tưởng cho các kỳ sau và xây dựng cơ chế backup tự động hàng ngày.", items)
            .Should().BeFalse();
        CheckpointStatusLines.Contradicts("Nhóm sẽ nghiên cứu chức năng trao đổi giữa mentor doanh nghiệp và giảng viên.", items).Should().BeFalse();
        CheckpointStatusLines.Contradicts("Đã hoàn thành backup tự động hàng ngày.", []).Should().BeFalse();
    }

    [Fact]
    public void TryBuild_DropsPointsThatContradictTheStatusLinesAndAnnotatesConflictingFigures()
    {
        const string json = """
            {"overview":"Slide review của nhóm.",
             "keyPoints":[
               "Đã xác nhận thêm chức năng trao đổi giữa mentor doanh nghiệp và giảng viên đứng lớp.",
               "Kho lưu trữ project nằm trong scope và đã được xây dựng.",
               "Tỷ lệ chuyển đổi sang Premium là 30% trong năm đầu."],
             "questions":[]}
            """;
        var preview = new WorkspaceFilePreview
        {
            StatusItems = CheckpointStatusLines.Find(ReviewSlide),
            Conflicts =
            [
                new WorkspaceFigureConflict
                {
                    Subject = "Conversion rate",
                    Values =
                    [
                        new WorkspaceFigureValue { Value = "15%", Quote = "750 Premium Subscribers (15% conversion rate)" },
                        new WorkspaceFigureValue { Value = "30%", Quote = "Premium Users 600 (Year 1) 30% conversion target" }
                    ]
                }
            ]
        };
        var grounding = new CheckpointSummaryGrounding(
            ReviewSlide + "\n750 Premium Subscribers (15% conversion rate)\nPremium Users 600 (Year 1) 30% conversion target\nChuyển đổi sang Premium trong năm đầu");

        var content = CheckpointSummaryOutput.TryBuild(json, preview, grounding)!;

        var points = content.KeyPoints.ToArray();
        points.Should().HaveCount(2);
        points.Should().NotContain(point => point.Contains("Đã xác nhận"));
        points[0].Should().Contain("Kho lưu trữ");
        points[1].Should().Be("Tỷ lệ chuyển đổi sang Premium là 30% trong năm đầu (tài liệu cũng ghi 15%).");
    }

    [Fact]
    public void Annotate_LeavesSentencesAloneWhenTheyNameNoConflictingMeasure()
    {
        var conflicts = new[]
        {
            new WorkspaceFigureConflict
            {
                Subject = "Conversion rate",
                Values =
                [
                    new WorkspaceFigureValue { Value = "15%", Quote = "a" },
                    new WorkspaceFigureValue { Value = "30%", Quote = "b" }
                ]
            }
        };

        CheckpointFigureConflicts.Annotate("Biên lợi nhuận khoảng 85%.", conflicts).Should().Be("Biên lợi nhuận khoảng 85%.");
        // A sentence that already gives both values needs no note.
        CheckpointFigureConflicts.Annotate("Tỷ lệ chuyển đổi 15% hoặc 30%.", conflicts).Should().Be("Tỷ lệ chuyển đổi 15% hoặc 30%.");
    }

    [Fact]
    public void Grounding_RejectsAFigureThatOnlyAppearsInAQuotedSlogan()
    {
        var grounding = new CheckpointSummaryGrounding(
            "Short-form videos về problem–solution\n" +
            "“5 lỗi formatting sinh viên gặp mỗi tuần”\n" +
            "82% identified document formatting as a major challenge.\n" +
            "104 university students surveyed.");

        grounding.QuotesSlogan("Vấn đề: Sinh viên gặp 5 lỗi formatting mỗi tuần khi làm báo cáo.").Should().BeTrue();
        grounding.Supports("Vấn đề: Sinh viên gặp 5 lỗi formatting mỗi tuần khi làm báo cáo.").Should().BeFalse();
        // The same kind of figure stated as a finding stays.
        grounding.QuotesSlogan("Khảo sát 104 university students cho thấy 82% gặp vấn đề.").Should().BeFalse();
    }
}
