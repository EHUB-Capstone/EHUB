using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Features.ProposalAnalyses;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProposalAnalyses;

public sealed class ProposalAnalysisOutputValidatorTests
{
    [Fact]
    public void Validate_AcceptsCandidateBoundGroundedOutput()
    {
        var (request, response) = ValidCase();

        var action = () => ProposalAnalysisOutputValidator.Validate(request, response);

        action.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsCandidateThatWasNotSupplied()
    {
        var (request, response) = ValidCase();
        response = response with
        {
            Matches = [response.Matches.Single() with { ProposalVersionId = Guid.NewGuid() }]
        };

        var action = () => ProposalAnalysisOutputValidator.Validate(request, response);

        AssertInvalid(action);
    }

    [Fact]
    public void Validate_RejectsFabricatedEvidence()
    {
        var (request, response) = ValidCase();
        var match = response.Matches.Single();
        response = response with
        {
            Matches = [match with
            {
                Evidence = [new ProposalAnalysisProviderEvidence(
                    ProposalAnalysisEvidenceSource.Candidate,
                    "Thông tin không tồn tại trong proposal")]
            }]
        };

        var action = () => ProposalAnalysisOutputValidator.Validate(request, response);

        AssertInvalid(action);
    }

    [Theory]
    [InlineData("The lecturer should reject this proposal.")]
    [InlineData("Đề nghị phê duyệt đề xuất này.")]
    [InlineData("Đây là đạo văn.")]
    public void Validate_RejectsAcademicOrPlagiarismVerdicts(string verdict)
    {
        var (request, response) = ValidCase();
        response = response with { Summary = verdict };

        var action = () => ProposalAnalysisOutputValidator.Validate(request, response);

        AssertInvalid(action);
    }

    private static (ProposalAnalysisProviderRequest Request, ProposalAnalysisProviderResponse Response) ValidCase()
    {
        var candidateId = Guid.NewGuid();
        var current = new ProjectProposalSnapshotDto
        {
            Problem = "Sinh viên gặp khó khăn khi tìm phòng trọ phù hợp.",
            Solution = "Nền tảng đề xuất phòng dựa trên nhu cầu."
        };
        var candidate = new ProjectProposalSnapshotDto
        {
            Problem = "Sinh viên cần tìm chỗ ở có mức giá phù hợp.",
            Solution = "Cổng thông tin tổng hợp phòng cho thuê."
        };
        var request = new ProposalAnalysisProviderRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            current,
            ProjectProposalAnalysisCandidateScope.AllSystem,
            true,
            ProjectProposalAnalysisLanguageMode.VietnameseAndEnglish,
            "proposal-analysis-config-v2",
            [new ProposalAnalysisProviderCandidate(
                candidateId,
                candidate,
                0.8,
                new ProposalAnalysisProviderFieldScores(0.9, 0.7, 0.8, 0.6),
                0.76,
                0.5,
                0.4,
                0.672)]);
        var response = new ProposalAnalysisProviderResponse(
            "Hai đề xuất cùng giải quyết nhu cầu tìm chỗ ở của sinh viên.",
            ProjectProposalOverlapRisk.Medium,
            ["Cơ chế đề xuất dựa trên nhu cầu là điểm khác biệt tiềm năng."],
            ["Kết quả chỉ dựa trên các proposal được cung cấp."],
            [new ProposalAnalysisProviderMatch(
                candidateId,
                ["Cùng tập trung vào nhu cầu chỗ ở của sinh viên."],
                ["Cách tiếp cận giải pháp khác nhau."],
                ["Đề xuất cá nhân hóa là yếu tố mới tiềm năng."],
                [
                    new ProposalAnalysisProviderEvidence(
                        ProposalAnalysisEvidenceSource.Current,
                        "Sinh viên gặp khó khăn khi tìm phòng trọ"),
                    new ProposalAnalysisProviderEvidence(
                        ProposalAnalysisEvidenceSource.Candidate,
                        "Sinh viên cần tìm chỗ ở")
                ])],
            "GoogleGemini",
            "gemini-2.5-flash",
            "proposal-comparative-analysis-prompt-v1",
            "proposal-analysis-result-v2");
        return (request, response);
    }

    private static void AssertInvalid(Action action)
    {
        var exception = action.Should().Throw<ProposalAnalysisProcessingException>().Which;
        exception.ErrorCode.Should().Be("PROPOSAL_ANALYSIS_OUTPUT_INVALID");
        exception.IsTransient.Should().BeTrue();
    }
}
