using EHub.Application.Common.Interfaces.AI;
using EHub.Domain.Enums;

namespace EHub.Infrastructure.Services;

public sealed class MockProposalAnalysisProvider : IProposalAnalysisProvider
{
    public Task<ProposalAnalysisProviderResponse> AnalyzeAsync(
        ProposalAnalysisProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var proposalName = string.IsNullOrWhiteSpace(request.Proposal.StartupName)
            ? request.Proposal.Title.Trim()
            : request.Proposal.StartupName.Trim();
        var differentiators = new List<string>();
        AddIfPresent(differentiators, request.Proposal.ValueProposition, "Đề xuất giá trị đã nêu");
        AddIfPresent(differentiators, request.Proposal.Technology, "Hướng công nghệ đã nêu");
        AddIfPresent(differentiators, request.Proposal.TargetCustomers, "Nhóm khách hàng mục tiêu đã nêu");
        if (differentiators.Count == 0)
            differentiators.Add("Cần bổ sung dữ liệu để xác định điểm khác biệt tiềm năng.");

        var retrievalSummary = request.RetrievalCandidates.Count == 0
            ? "Chưa có proposal lịch sử hợp lệ để đối sánh."
            : $"Đã truy xuất {request.RetrievalCandidates.Count} proposal và xếp hạng theo semantic có trọng số; mức cao nhất là {Math.Max(0, request.RetrievalCandidates[0].WeightedSemanticSimilarity):P1}.";

        return Task.FromResult(new ProposalAnalysisProviderResponse(
            $"Báo cáo retrieval đã tiếp nhận đề xuất {proposalName}. {retrievalSummary}",
            ProjectProposalOverlapRisk.InsufficientData,
            differentiators,
            [
                "Đây là kết quả mô phỏng, không phải kết luận về mức độ trùng lặp hoặc đạo văn.",
                "Đã áp dụng embedding, cosine similarity theo từng trường và weighted semantic ranking; chưa có lexical/hybrid ranking hoặc diễn giải bằng LLM.",
                "Kết quả chỉ hỗ trợ tham khảo và không tự động quyết định duyệt hoặc từ chối đề xuất."
            ],
            "Mock",
            "deterministic-v1",
            "proposal-analysis-mock-v1",
            "proposal-analysis-result-v1"));
    }

    private static void AddIfPresent(List<string> output, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var normalized = value.Trim();
        output.Add($"{label}: {normalized[..Math.Min(normalized.Length, 220)]}");
    }
}
