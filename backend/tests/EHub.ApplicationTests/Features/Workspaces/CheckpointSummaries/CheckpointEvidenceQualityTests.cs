using System.Text;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Application.Features.Mentoring.Evidence;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>Problems seen in four real checkpoint files (logistics survey, F&amp;B shift app, Rolly market report).</summary>
public sealed class CheckpointEvidenceQualityTests
{
    private static ChecklistItem Criterion(int index, string name, string excellent) =>
        new(index, "Criterion", name, excellent, 20, CheckpointCriterionProfile.Classify(name), excellent);

    private static readonly ChecklistItem Target =
        Criterion(0, "Target Customer Definition", "Target customer is specific, well-segmented, and supported by data");

    private static readonly ChecklistItem ProblemAnalysis =
        Criterion(1, "Market Problem Analysis", "Market problem is analyzed deeply with credible evidence");

    private static readonly ChecklistItem ProblemSolution =
        Criterion(3, "Problem–Solution Fit", "Solution clearly solves the identified problem");

    private static WorkspaceFileRequirementFindingView Find(string text, ChecklistItem item)
    {
        var finding = CheckpointSummaryEvidence.Find(text, [item], CheckpointSummaryTopics.None)[0];
        return new WorkspaceFileRequirementFindingView(finding.Finding, finding.Evidence);
    }

    private sealed record WorkspaceFileRequirementFindingView(string Finding, string? Evidence);

    [Fact]
    public void Find_ReportsAMentionedThingAsRelatedInsteadOfContradictingItself()
    {
        // Only a slide title names the customers: the cue is found, so the item cannot also be "not found".
        const string deck = "TARGET CUSTOMERS\nStudents in Hanoi who write essays every week.\nA short closing note about the schedule.";

        var result = Find(deck, Target);

        result.Finding.Should().Be(CheckpointSummaryEvidence.Related);
        result.Evidence.Should().Contain("TARGET CUSTOMERS");
    }

    [Fact]
    public void Find_StillReportsNotFoundWhenNothingIsMentioned()
    {
        Find("Lịch họp nhóm vào thứ hai hàng tuần, địa điểm phòng lab.", Target).Finding.Should().Be(CheckpointSummaryEvidence.NotFound);
    }

    [Fact]
    public void Find_DoesNotQuoteChartLabelsGluedTogether()
    {
        const string deck =
            "TỔNG SỐ PHẢN HỒI SẴN SÀNG DÙNG THỬ TRA CỨU > 10 PHÚT RỦI RO HẢI QUAN CAO NHẤT\n" +
            "82,7% doanh nghiệp mất từ 11 đến hơn 30 phút để tìm bộ chứng từ cũ, rủi ro lớn nhất là thiếu sót chứng từ khi kiểm tra Hải quan.";

        Find(deck, ProblemAnalysis).Evidence.Should().StartWith("82,7% doanh nghiệp");
    }

    [Fact]
    public void Find_DoesNotQuoteAdviceAsTheMarketProblem()
    {
        const string deck =
            "Strategic implication: Nên vào thị trường bằng pilot hẹp 6–8 tuần cho 3–5 kịch bản rủi ro cao.\n" +
            "Nhân viên mới mất 3 tuần mới làm quen quy trình, gây rủi ro sai sót với khách hàng luxury.";

        Find(deck, ProblemAnalysis).Evidence.Should().StartWith("Nhân viên mới mất 3 tuần");
    }

    [Fact]
    public void Find_StillQuotesAdviceForACriterionAboutTheSolution()
    {
        var solution = Criterion(2, "Solution Roadmap", "Clear roadmap and solution for the problem");
        const string deck = "Strategic implication: Nên triển khai giải pháp theo lộ trình pilot hẹp 6–8 tuần cho vấn đề đào tạo.";

        Find(deck, solution).Evidence.Should().StartWith("Strategic implication");
    }

    [Fact]
    public void Find_DoesNotQuoteACompetitorRowAsTheSolution()
    {
        const string deck =
            "Trainual Giải pháp thay thế Custom quote SOP và AI knowledge search cho vấn đề đào tạo.\n" +
            "Giải pháp của Rolly giúp nhà hàng rút ngắn thời gian đào tạo nhân viên mới, giải quyết vấn đề đào tạo thủ công.";

        Find(deck, ProblemSolution).Evidence.Should().StartWith("Giải pháp của Rolly");
    }

    [Fact]
    public void Find_PrefersALineWithContentOverAHeadingFollowedByTableCells()
    {
        const string deck =
            "4. Customer Segments & Insights\nQuy mô\nNhu cầu/JTB\nD\n" +
            "Khách hàng mục tiêu chính: nhà hàng fine-dining độc lập có 30-80 nhân sự cần đào tạo liên tục.";

        Find(deck, Target).Evidence.Should().StartWith("Khách hàng mục tiêu chính");
    }

    [Fact]
    public void Find_ReportsAHeadingWithOnlyTableCellsAsMentionedWithoutAQuote()
    {
        // The file has a customer segments section, but its text is a table header: nothing worth quoting.
        const string deck = "4. Customer Segments & Insights\nSegment Quy mô/giá trị\nNhu cầu/JTB\nD";

        var finding = CheckpointSummaryEvidence.Find(deck, [Target], CheckpointSummaryTopics.None)[0];

        finding.Finding.Should().Be(CheckpointSummaryEvidence.Related);
        finding.Evidence.Should().BeNull();
        finding.Cues.Should().ContainSingle(cue => cue.Label == "Target customer" && cue.Found && cue.Evidence == null);
    }

    [Fact]
    public void Find_DoesNotQuoteAChartsLabelsReadAsOneLine()
    {
        const string deck =
            "52 Doanh nghiệp tham gia 98% Doanh nghiệp khảo sát 82,7% Mất nhiều thời gian tra cứu 4,35/5.0 Điểm rủi ro cao nhất\n" +
            "82,7% doanh nghiệp mất từ 11 đến hơn 30 phút để tìm bộ chứng từ cũ, rủi ro lớn nhất là thiếu sót chứng từ.";

        Find(deck, ProblemAnalysis).Evidence.Should().StartWith("82,7% doanh nghiệp mất từ 11");
        CheckpointSummaryText.IsLabelStrip("52 Doanh nghiệp tham gia 98% Doanh nghiệp khảo sát 82,7% Mất nhiều thời gian").Should().BeTrue();
        CheckpointSummaryText.IsLabelStrip("Chi phí tăng 15% so với năm 2025 do giá nguyên liệu tăng.").Should().BeFalse();
    }

    [Fact]
    public void Find_SkipsNumberedTopicsAndOptionsListedUnderThem()
    {
        const string deck =
            "7. Vấn đề mà công cụ hiện tại chưa xử lý được.\n6.Công cụ quản lý chứng từ hiện tại.\n" +
            "Nhân viên mất 15 phút tìm một bộ chứng từ cũ, đây là vấn đề lớn nhất của khách hàng.";

        Find(deck, ProblemAnalysis).Evidence.Should().StartWith("Nhân viên mất 15 phút");
        CheckpointSummaryText.IsNumberedLabel("9. Đánh giá ý tưởng, chức năng nào tạo ra giá trị rõ ràng.").Should().BeTrue();
        CheckpointSummaryText.IsNumberedLabel("1. Hơn 80% khách hàng cho biết mất 15 phút mỗi lần.").Should().BeFalse();
    }

    [Fact]
    public void Find_NeverQuotesDashboardInstructions()
    {
        const string deck =
            "🎯 CHỌN LOẠI HÌNH DOANH NGHIỆP 💡 Hướng dẫn tương tác: Chọn loại hình tại menu thả xuống bên trái để xem phân tích nghiên cứu\n" +
            "Dữ liệu nghiên cứu từ 52 doanh nghiệp Forwarder, 3PL, Express và XNK qua khảo sát bảng hỏi.";
        var research = Criterion(2, "Market Research Quality", "Research uses multiple credible methods and sources");

        Find(deck, research).Evidence.Should().StartWith("Dữ liệu nghiên cứu từ 52");
    }

    [Fact]
    public void Find_DoesNotQuoteTheSameSentenceForTwoCriteriaWhenNothingElseMatches()
    {
        const string deck = "Quản lý còn thủ công; vấn đề lớn nhất là nhân viên nghỉ hoặc đổi ca đột xuất (70%).";

        var findings = CheckpointSummaryEvidence.Find(deck, [ProblemAnalysis, ProblemSolution], CheckpointSummaryTopics.None);

        findings[0].Evidence.Should().Contain("vấn đề lớn nhất");
        findings[1].Evidence.Should().BeNull("the second criterion has no passage of its own");
        findings[1].Finding.Should().Be(CheckpointSummaryEvidence.Related, "its cue for the problem is still found");
    }

    [Fact]
    public void Find_QuotesALineThatIntroducesAListTogetherWithTheList()
    {
        const string deck =
            "Interview Kiểm chứng pain point, buying process, 12–15 người:\n" +
            "Quản lý nhà hàng cho biết nhân viên mới học việc rất chậm và dễ sai sót.";

        Find(deck, ProblemAnalysis).Evidence.Should().Contain("Interview").And.Contain("học việc rất chậm");
    }

    [Fact]
    public void Find_RecognisesACustomerBaseWrittenAsATargetGroup()
    {
        CheckpointCriterionCues.Mentions("Target customer", MentorEvidenceText.Normalize("Tệp 4.500 doanh nghiệp SME Forwarder tại Việt Nam."))
            .Should().BeTrue();
    }

    [Fact]
    public void Tidy_JoinsLinesThatEndOnAConnectiveAndRepairsSyllablesBroughtTogetherByTheJoin()
    {
        CheckpointLayoutText.Tidy("Đánh giá khả năng cho nền tảng đào tạo mới bằng\nScenario Role-play.", layoutText: true)
            .Should().Be("Đánh giá khả năng cho nền tảng đào tạo mới bằng Scenario Role-play.");
        CheckpointLayoutText.Tidy("Quy mô mẫu hợp lệ: N = 51 nhân sự trong lĩnh vực Logistics\n& Freight Forwarding.", layoutText: true)
            .Should().Be("Quy mô mẫu hợp lệ: N = 51 nhân sự trong lĩnh vực Logistics & Freight Forwarding.");
        // The syllables are on three lines in the PDF; the join makes "gi á trị", which must then be repaired.
        CheckpointLayoutText.Tidy("Segment Quy mô/gi\ná\ntrị", layoutText: true).Should().Be("Segment Quy mô/giá trị");
    }

    [Fact]
    public void Highlights_SkipALineAboutSomeonesYearsOfExperience()
    {
        const string text =
            "7 năm kinh nghiệm thực chiến (từ 2019) trong lĩnh vực logistics và thủ tục hải quan.\n" +
            "Lãng phí vô hình: 84,3% nhân sự tốn hơn 10 phút chỉ để tìm lại một bộ chứng từ cũ.";

        CheckpointSummaryText.Highlights(text, ["Market problem"], 1).Should().ContainSingle().Which.Should().StartWith("Lãng phí vô hình");
    }

    [Fact]
    public void Find_RecognisesACompetitiveAdvantageSectionAsTheSolution()
    {
        const string deck =
            "Lợi thế cạnh tranh của OPS\n" +
            "Tự ghép nhân viên kinh nghiệm với nhân viên mới giờ cao điểm.\n" +
            "Cảnh báo đỏ khi chi phí lương vượt ngưỡng an toàn.\n" +
            "Quản lý còn thủ công; vấn đề lớn nhất là nhân viên nghỉ hoặc đổi ca đột xuất (70%).";

        var cues = CheckpointSummaryEvidence.Find(deck, [ProblemSolution], CheckpointSummaryTopics.None)[0].Cues;

        cues.Should().Contain(cue => cue.Label == "Solution / value" && cue.Found);
    }

    [Fact]
    public void Highlights_SkipInterviewTopicsAndAChartsLabels()
    {
        const string text =
            "1. Phạm vi trách nhiệm công việc hằng ngày của chuyên viên.\n" +
            "2. Quy trình quản lý 1 bộ chứng từ từ lúc bắt đầu đến lúc cất giữ.\n" +
            "3. Cách truy xuất 1 bộ chứng từ cũ và thời gian tiêu tốn.\n" +
            "52 Doanh nghiệp tham gia 98% Doanh nghiệp khảo sát 82,7% Mất nhiều thời gian tra cứu 4,35/5.0 Điểm rủi ro cao nhất\n" +
            "Lãng phí vô hình: 84,3% nhân sự tốn hơn 10 phút chỉ để tìm lại một bộ chứng từ cũ.";

        CheckpointSummaryText.Highlights(text, ["Market problem"], 1).Should().ContainSingle().Which.Should().StartWith("Lãng phí vô hình");
    }

    [Fact]
    public void Covered_ListsOnlyTheCriteriaTheFileWasFoundToCover()
    {
        ChecklistItem[] checklist = [Target, ProblemAnalysis, Criterion(2, "Market Research Quality", "Research uses multiple credible methods")];
        const string canvas = "Phân khúc khách hàng: sinh viên ký túc xá đặt đồ ăn 4–7 lần mỗi tuần.\nTệp 4.500 doanh nghiệp SME Forwarder tại Việt Nam.";

        var findings = CheckpointSummaryEvidence.Find(canvas, checklist, CheckpointSummaryTopics.None);
        var covered = CheckpointSummaryEvidence.Covered(checklist, findings);

        covered.Select(item => item.Title).Should().Contain("Target Customer Definition").And.NotContain("Market Research Quality");
    }

    [Theory]
    [InlineData("Tài liệu chưa nêu rõ quy mô thị trường (TAM, SAM).", null)]
    [InlineData("Tài liệu có đề cập đến chi tiết về tính năng này không?", null)]
    [InlineData("Không có thông tin chi tiết về lịch trình triển khai dự án.", null)]
    [InlineData("Nhóm sẽ kiểm chứng giá 59.000 đồng với khách hàng nào", null)]
    [InlineData("Theo tài liệu, năm đầu lỗ 180 triệu đồng; nhóm đã tính chi phí này chưa?", "Năm đầu lỗ 180 triệu đồng; nhóm đã tính chi phí này chưa?")]
    [InlineData("Nhóm đã xác định phân khúc khách hàng chính chưa?", "Nhóm đã xác định phân khúc khách hàng chính chưa?")]
    public void AsTeamQuestion_KeepsOnlyQuestionsForTheTeam(string question, string? expected)
    {
        CheckpointSummaryText.AsTeamQuestion(question).Should().Be(expected);
    }

    [Fact]
    public void TryBuild_DropsStatementsAboutTheDocumentFromTheQuestions()
    {
        const string answer = """
            {"overview":"Báo cáo của nhóm EcoBag.","keyPoints":[],"questions":["Không có thông tin chi tiết về lịch trình triển khai.","Nhóm sẽ bán thử ở đâu trước?"]}
            """;

        var content = CheckpointSummaryOutput.TryBuild(answer, new EHub.Contracts.Workspaces.WorkspaceFilePreview());

        content!.Questions.Should().Equal("Nhóm sẽ bán thử ở đâu trước?");
    }

    [Fact]
    public void Find_DoesNotReportAWholeFileCriterionAsMissing()
    {
        var clarity = Criterion(0, "Startup Idea Clarity", "Idea is innovative, clearly articulated, and easy to understand");

        var finding = CheckpointSummaryEvidence.Find("Lịch họp nhóm vào thứ hai hàng tuần tại phòng lab.", [clarity], CheckpointSummaryTopics.None)[0];

        clarity.Assessment.Should().Be(CheckpointCriterionProfile.Overall);
        finding.Finding.Should().Be(CheckpointSummaryEvidence.Overall);
        CheckpointSummaryOutput.Combine([clarity], [("a.pdf", [finding])])[0].Finding.Should().Be(CheckpointSummaryEvidence.Overall);
    }

    [Fact]
    public void Find_DoesNotCountAPromiseToWriteItLaterAsTheRoadmap()
    {
        var roadmap = Criterion(0, "Product Roadmap & Core Features", "Clear, realistic roadmap; well-prioritized core features");

        CheckpointSummaryEvidence.Find("Nhóm sẽ hoàn thiện lộ trình và các phần còn lại sau.", [roadmap], CheckpointSummaryTopics.None)[0]
            .Finding.Should().Be(CheckpointSummaryEvidence.NotFound);
        CheckpointSummaryText.IsDeferral(MentorEvidenceText.Normalize("Nhóm đang hoàn thiện nội dung.")).Should().BeTrue();
        CheckpointSummaryText.IsDeferral(MentorEvidenceText.Normalize("Quý 4/2026: hoàn thiện MVP gồm tạo bộ thẻ.")).Should().BeFalse();
    }

    [Theory]
    [InlineData("Business model", "Thu phí thuê bao 59.000 đồng mỗi tháng cho mỗi gia đình.", true)]
    [InlineData("Business model", "Nguồn thu chính là phí hoa hồng từ tiệm spa đối tác.", true)]
    [InlineData("Business model", "Giá thuê là 3.000 đồng mỗi 10 phút và gói tháng 149.000 đồng.", true)]
    [InlineData("Business model", "Giá tour 650.000 đồng/khách.", true)]
    [InlineData("Business model", "Sinh viên mở khóa xe bằng mã QR và trả phí theo phút.", true)]
    [InlineData("Business model", "Lịch họp nhóm vào thứ hai hàng tuần tại phòng lab.", false)]
    [InlineData("Problem", "45 người cho biết từng phải chờ quá 1 giờ khi đưa thú cưng đi tắm.", true)]
    [InlineData("Problem", "Quản lý mất nhiều thời gian xếp ca mỗi tuần.", true)]
    [InlineData("Problem", "Idea and execution show creative problem-solving.", false)]
    [InlineData("Solution / value", "Nhắc học giải quyết việc quên lịch ôn.", true)]
    [InlineData("Product / demo", "Hai nhà đầu tư thiên thần đã gặp tại sự kiện demo day.", false)]
    [InlineData("Product / demo", "Bản demo chạy trên Android với 9 màn hình.", true)]
    public void Cues_RecogniseHowStudentsActuallyPhraseIt(string label, string sentence, bool expected)
    {
        CheckpointCriterionCues.Mentions(label, MentorEvidenceText.Normalize(sentence)).Should().Be(expected);
    }

    [Fact]
    public void Grounding_RejectsARangeWhoseEndsTheFileNeverWritesTogether()
    {
        var grounding = new CheckpointSummaryGrounding("Nhập liệu thủ công tốn 15 phút một bộ. Tìm file mất 20-30' mỗi lần.");

        grounding.Supports("Tìm file tốn trung bình 20-30 phút mỗi lần.").Should().BeTrue();
        grounding.Supports("Tìm file tốn trung bình 15-30 phút mỗi lần.").Should().BeFalse();
    }

    [Fact]
    public void Grounding_AsksMoreOfAKeyPointAboutAFileThatIsMostlyImages()
    {
        const string source = "Xếp ca thủ công bằng Excel";
        const string point = "Xếp ca thủ công dự báo nhu cầu";

        new CheckpointSummaryGrounding(source).Supports(point).Should().BeTrue();
        new CheckpointSummaryGrounding(source, littleText: true).Supports(point).Should().BeFalse();
    }

    [Theory]
    [InlineData("quyết định số nhân sự bằ ng kinh nghiệm", "quyết định số nhân sự bằng kinh nghiệm")]
    [InlineData("Segment Quy mô/gi á trị", "Segment Quy mô/giá trị")]
    [InlineData("Phân khúc ban đầ u: F&B nhiều chi nhánh", "Phân khúc ban đầu: F&B nhiều chi nhánh")]
    [InlineData("Khách sống ở Hà Nội và dùng ứng dụng", "Khách sống ở Hà Nội và dùng ứng dụng")]
    public void Tidy_RepairsSyllablesTheFontSplitApart(string line, string expected)
    {
        CheckpointLayoutText.Tidy(line, layoutText: false).Should().Be(expected);
    }

    [Fact]
    public void Tidy_RepairsSyllablesWhoseToneMarkIsAStandaloneCodePoint()
    {
        // "bằ ng" typed as b + a + breve + grave (decomposed) must still be joined.
        var decomposed = "nhân sự b" + "ằ" + " ng kinh nghiệm".Normalize(NormalizationForm.FormD);

        CheckpointLayoutText.Tidy(decomposed, layoutText: false).Should().Contain("bằng kinh nghiệm");
    }

    [Fact]
    public void AssumesUnseenFact_DropsQuestionsBuiltOnFiguresOrContrastsTheFileNeverMakes()
    {
        var grounding = new CheckpointSummaryGrounding(
            "82,7% doanh nghiệp mất từ 11 đến hơn 30 phút để tìm bộ chứng từ cũ.\n98% doanh nghiệp sẵn sàng dùng thử.");

        grounding.AssumesUnseenFact("Tại sao 91% doanh nghiệp ngại học công cụ mới?").Should().BeTrue();
        grounding.AssumesUnseenFact("Vì sao thời gian tra cứu cao nhất trong khi các doanh nghiệp khác lại chỉ mất vài phút?").Should().BeTrue();
        grounding.AssumesUnseenFact("Why do firms lose time while other firms need only minutes?").Should().BeTrue();
        grounding.AssumesUnseenFact("Nhóm kiểm chứng thế nào con số 82,7% doanh nghiệp mất hơn 11 phút?").Should().BeFalse();
        grounding.AssumesUnseenFact("Nhóm định giá bản dùng thử ra sao?").Should().BeFalse();
    }

    [Fact]
    public void TryBuild_KeepsOnlyQuestionsTheFileCanBackUp()
    {
        var grounding = new CheckpointSummaryGrounding("52 doanh nghiệp tham gia khảo sát, 98% sẵn sàng dùng thử.");
        const string answer = """
            {"overview":"Báo cáo khảo sát 52 doanh nghiệp.","keyPoints":[],"questions":["Tại sao 70% doanh nghiệp bỏ cuộc?","Nhóm chọn 52 doanh nghiệp theo tiêu chí nào?"]}
            """;

        var content = CheckpointSummaryOutput.TryBuild(answer, new EHub.Contracts.Workspaces.WorkspaceFilePreview(), grounding);

        content!.Questions.Should().ContainSingle().Which.Should().Contain("52 doanh nghiệp");
    }

    [Fact]
    public void CueNormalisation_StillMatchesWhatTheFirstRoundExpects()
    {
        // The competitor concept now also knows "giải pháp thay thế" (alternative solutions).
        CheckpointCriterionCues.Mentions("Competitors", MentorEvidenceText.Normalize("Trainual Giải pháp thay thế")).Should().BeTrue();
        CheckpointCriterionCues.Mentions("Competitors", MentorEvidenceText.Normalize("Giải pháp của Rolly")).Should().BeFalse();
    }
}
