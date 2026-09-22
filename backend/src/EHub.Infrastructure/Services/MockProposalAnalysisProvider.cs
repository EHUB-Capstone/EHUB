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

        return Task.FromResult(new ProposalAnalysisProviderResponse(
            $"Báo cáo mô phỏng đã tiếp nhận đề xuất {proposalName}. Giai đoạn này chỉ kiểm tra luồng xử lý và cấu trúc báo cáo; chưa so sánh ngữ nghĩa với kho dự án lịch sử.",
            ProjectProposalOverlapRisk.InsufficientData,
            differentiators,
            [
                "Đây là kết quả mô phỏng, không phải kết luận về mức độ trùng lặp hoặc đạo văn.",
                "Chưa áp dụng embedding, cosine similarity hoặc truy xuất dự án tương đồng.",
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
