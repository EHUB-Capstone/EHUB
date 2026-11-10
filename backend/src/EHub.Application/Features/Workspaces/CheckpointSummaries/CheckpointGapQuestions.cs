using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Questions for the team worked out from what the file leaves out, without the model: when a rubric criterion is covered by
/// the file but one of the things its Excellent level asks for (a customer journey, a roadmap...) is not mentioned, the
/// reviewer is offered a question about that one thing. The wording is fixed, so it cannot invent a premise the way a small
/// model's questions sometimes do, and it names the criterion so the reviewer knows why it is asked. A thing the file calls
/// by another name is asked about too, which is why the question is a question and not a finding.
/// </summary>
internal static class CheckpointGapQuestions
{
    // How the things a rubric asks for are named in Vietnamese. Every concept of CheckpointCriterionCues needs an entry.
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["Roadmap"] = "lộ trình phát triển",
        ["Core features"] = "tính năng cốt lõi",
        ["Prioritization"] = "thứ tự ưu tiên tính năng",
        ["User needs"] = "nhu cầu của người dùng",
        ["Persona"] = "chân dung khách hàng (persona)",
        ["Target customer"] = "khách hàng mục tiêu",
        ["Customer journey (CJM)"] = "hành trình khách hàng (CJM)",
        ["Marketing plan"] = "kế hoạch marketing",
        ["Product / demo"] = "sản phẩm mẫu hoặc bản demo",
        ["UI/UX"] = "giao diện và trải nghiệm người dùng",
        ["Stability"] = "độ ổn định của sản phẩm",
        ["Problem"] = "vấn đề của khách hàng",
        ["Solution / value"] = "giải pháp và giá trị mang lại",
        ["Case requirements"] = "yêu cầu của đề bài",
        ["Launch"] = "kế hoạch ra mắt",
        ["Feedback"] = "phản hồi từ người dùng",
        ["Real users"] = "người dùng thật",
        ["Sales"] = "doanh thu hoặc đơn hàng",
        ["Proof (bills, screenshots)"] = "bằng chứng (hóa đơn, ảnh chụp màn hình)",
        ["Usage data"] = "số liệu sử dụng",
        ["Insights / lessons"] = "bài học rút ra",
        ["Business model"] = "mô hình kinh doanh",
        ["Canvas (BMC)"] = "Business Model Canvas",
        ["Market size"] = "quy mô thị trường",
        ["Competitors"] = "đối thủ cạnh tranh",
        ["Research"] = "kết quả nghiên cứu hoặc khảo sát",
        ["Financials"] = "số liệu tài chính",
        ["Team roles"] = "phân công vai trò",
        ["Innovation"] = "điểm khác biệt"
    };

    /// <summary>Concepts that have no Vietnamese name here yet; empty while the two lists agree (a test keeps them so).</summary>
    public static IReadOnlyList<string> UnnamedConcepts() =>
        CheckpointCriterionCues.Labels.Where(label => !Names.ContainsKey(label)).ToArray();

    /// <summary>
    /// At most <see cref="CheckpointSummaryLimits.MaxQuestions"/> questions, heaviest criterion first, one per missing thing.
    /// A file that is mostly images gets none: little of it was read, so "not mentioned" says nothing.
    /// </summary>
    public static IReadOnlyList<string> From(IReadOnlyCollection<WorkspaceFileRequirementFinding> findings, bool littleText)
    {
        if (littleText) return [];
        var asked = new HashSet<string>(StringComparer.Ordinal);
        var questions = new List<string>();
        foreach (var finding in findings
                     .Where(item => item.Kind == CheckpointSummarySource.CriterionKind &&
                                    item.Assessment == CheckpointCriterionProfile.Document &&
                                    item.Finding == CheckpointSummaryEvidence.Related)
                     .OrderByDescending(item => item.Weight ?? 0).ThenBy(item => item.Index))
        {
            // Only a criterion the file does cover: when it covers none of what the criterion asks for, "Not found" already says so.
            if (!finding.Cues.Any(cue => cue.Found)) continue;
            var missing = finding.Cues.FirstOrDefault(cue => !cue.Found && Names.ContainsKey(cue.Label) && !asked.Contains(cue.Label));
            if (missing is null) continue;
            asked.Add(missing.Label);
            questions.Add($"Nhóm đã có {Names[missing.Label]} chưa? File này chưa nhắc tới, trong khi tiêu chí \"{finding.Title}\" cần phần này.");
            if (questions.Count == CheckpointSummaryLimits.MaxQuestions) break;
        }
        return questions;
    }
}
