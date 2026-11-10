using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

// A deck that says nothing about competitors or the team's plan must not be shown as covering them just because it talks
// about price, users or stages. Text invented in the shape of persona/pitch decks seen in production.
public sealed class CheckpointNoCompetitorFindingsTests
{
    private static ChecklistItem Criterion(int index, string name, string excellent) =>
        new(index, "Criterion", name, excellent, 20, CheckpointCriterionProfile.Classify(name), excellent);

    private static readonly ChecklistItem[] Rubric =
    [
        Criterion(0, "Target Customer Definition", "Target customer is specific, well-segmented, and supported by data"),
        Criterion(1, "Competitor Analysis", "Direct and indirect competitors are analysed with clear differentiation"),
        Criterion(2, "Team Plan", "Clear work plan with roles and responsibilities for each team member")
    ];

    private const string PersonaDeck =
        "Quy mô thị trường: khoảng 90.000 sinh viên tại Đà Nẵng.\n" +
        "Vấn đề cốt lõi: lịch học thay đổi theo kỳ nên khó nhận ca cố định; tìm việc qua nhóm Facebook dễ bị bùng tiền.\n" +
        "Mức sẵn sàng trả phí: 19.000–59.000 đồng mỗi tháng nếu việc phù hợp.\n" +
        "Nam (21 tuổi), sinh viên năm 3, cần việc 2–4 tiếng mỗi ngày theo lịch học.\n" +
        "Hay xem nhóm Facebook tìm việc, nhạy cảm về giá và ngại trả phí trước.\n" +
        "Tải app, tạo hồ sơ, nhận 100 token. Dùng token ứng tuyển ca 2 tiếng. Được chấm 5 sao; mua gói 29k.";

    private const string GoToMarketDeck =
        "Giai đoạn 1: Thử nghiệm với 5 nhà thuốc quen biết, không thu phí.\n" +
        "Giai đoạn 2: Mở bán gói 99.000 đồng mỗi tháng cho nhà thuốc đã thử.\n" +
        "Giai đoạn 3: Giới thiệu bạn bè, nhận thêm 1 tháng sử dụng miễn phí.\n" +
        "Zalo OA: Reach và Product Demonstration. Facebook Group chủ nhà thuốc: Community. Landing Page: CTA đăng ký dùng thử.";

    [Theory]
    [InlineData(PersonaDeck)]
    [InlineData(GoToMarketDeck)]
    public void Find_DoesNotCallPriceOrPhasesACompetitorAnalysisOrATeamPlan(string deck)
    {
        var findings = CheckpointSummaryEvidence.Find(deck, Rubric, CheckpointSummaryTopics.None);

        findings[1].Finding.Should().NotBe(CheckpointSummaryEvidence.Related, "the deck names no competitor");
        findings[2].Finding.Should().NotBe(CheckpointSummaryEvidence.Related, "the deck assigns no work to anyone");
    }

    [Fact]
    public void Grounding_RejectsAPriceReadAsANumberOfPeople()
    {
        var grounding = new CheckpointSummaryGrounding(GoToMarketDeck);

        // The file says "gói 99.000 đồng mỗi tháng"; a small model wrote "99.000 người mỗi tháng".
        grounding.Supports("Mở rộng sang 99.000 người mỗi tháng sau giai đoạn thử nghiệm.").Should().BeFalse();
        grounding.Supports("Mở bán gói 99.000 đồng mỗi tháng sau giai đoạn thử nghiệm.").Should().BeTrue();
    }
}
