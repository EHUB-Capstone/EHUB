using System.Text.Json;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointSummaryReliabilityTests
{
    [Theory]
    [InlineData("Nhóm ghi nhận 20 khách hàng trả phí.", "Nhóm ghi nhận 20% khách hàng trả phí.", false)]
    [InlineData("Doanh thu thay đổi -20% trong quý.", "Doanh thu thay đổi 20% trong quý.", false)]
    [InlineData("Nhóm ghi nhận 12,5 phần trăm khách hàng trả phí.", "Nhóm ghi nhận 12.5% khách hàng trả phí.", true)]
    [InlineData("The team converted 25 percent of customers.", "The team converted 25% of customers.", true)]
    public void EvidenceMustSupportThePercentageAsWritten(string source, string claim, bool supported)
    {
        var evidence = new CheckpointSummaryGrounding(source).FindEvidence(claim);
        evidence.Should().Be(supported ? source : null);
    }

    [Fact]
    public void ClaimsNeedOneRelevantSourceAndExposeTheOriginalPageInsteadOfMixingFactsAcrossTheFile()
    {
        const string first = "Nhóm khảo sát 20 sinh viên tại Đà Nẵng.";
        const string second = "Nhóm phỏng vấn 5 chủ cửa hàng.";
        var source = first + "\n" + second;
        var pages = CheckpointPageLocator.From(new ExtractedText(source, 2, [first, second]));
        var answer = JsonSerializer.Serialize(new
        {
            overview = "Nhóm khảo sát 20 sinh viên.",
            keyPoints = new[] { "Nhóm khảo sát 20 sinh viên.", "Nhóm phỏng vấn 5 chủ cửa hàng.", "Nhóm khảo sát 5 sinh viên." },
            questions = Array.Empty<string>()
        });

        var content = CheckpointSummaryOutput.TryBuild(answer, new WorkspaceFilePreview { Highlights = [first, second] },
            new CheckpointSummaryGrounding(source), pages)!;

        content.KeyPoints.Should().HaveCount(2).And.NotContain("Nhóm khảo sát 5 sinh viên.");
        content.KeyPointEvidence.Select(item => item.Quote).Should().Equal(first, second);
        content.KeyPointEvidence.Select(item => item.Page).Should().Equal(1, 2);
        content.OverviewEvidence.Should().ContainSingle().Which.Quote.Should().Be(first);
    }

    [Fact]
    public void ChecklistSnapshotInvalidatesMeaningChangesEvenWhenTheCriterionTitleStaysTheSame()
    {
        ChecklistItem[] checklist = [new(0, "Criterion", "Persona", "Identify customers", 20, "Document", "Identify customers")];
        var stored = CheckpointSummarySnapshot.Serialize(new WorkspaceFilePreview(), checklist);
        CheckpointSummarySnapshot.Matches(stored, CheckpointSummarySnapshot.Fingerprint(checklist)).Should().BeTrue();
        foreach (var changed in new[]
        {
            checklist[0] with { Description = "Identify customers and validate their needs" },
            checklist[0] with { Excellent = "Include a customer journey map" },
            checklist[0] with { Weight = 30 },
            checklist[0] with { Assessment = "Product" }
        })
            CheckpointSummarySnapshot.Matches(stored, CheckpointSummarySnapshot.Fingerprint([changed])).Should().BeFalse();
        CheckpointSummarySnapshot.Matches("{}", CheckpointSummarySnapshot.Fingerprint(checklist)).Should().BeFalse();
        CheckpointSummarySnapshot.Matches("not json", CheckpointSummarySnapshot.Fingerprint(checklist)).Should().BeFalse();
    }

    [Theory]
    [InlineData("Nhóm khảo sát 20 khách hàng về ứng dụng đặt lịch.", "Nhóm đã có 9999 người dùng trả phí.")]
    [InlineData("Ngân sách triển khai là 2,5 triệu đồng.", "Ngân sách triển khai là 2,5 triệu USD.")]
    [InlineData("Quy mô thị trường tiềm năng là 2 triệu người dùng.", "Mục tiêu của nhóm là thu hút 2 triệu người dùng.")]
    public void OverviewNeverKeepsAnUnsupportedFirstSentence(string source, string invented)
    {
        new CheckpointSummaryGrounding(source).GroundedOverview(invented).Should().BeEmpty();
    }

    [Fact]
    public void UnusableOverviewFallsBackToARealQuoteInsteadOfTheFabricatedAnswer()
    {
        const string source = "Nhóm khảo sát 20 khách hàng về ứng dụng đặt lịch.";
        var preview = new WorkspaceFilePreview { Highlights = [source] };
        var answer = JsonSerializer.Serialize(new { overview = "Nhóm đã có 9999 người dùng trả phí.", keyPoints = new[] { "Có 9999 người dùng." }, questions = Array.Empty<string>() });

        var content = CheckpointSummaryOutput.TryBuild(answer, preview, new CheckpointSummaryGrounding(source));

        content.Should().NotBeNull();
        content!.Overview.Should().Contain(source).And.NotContain("9999");
        content.KeyPoints.Should().BeEmpty();
    }
}
