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
    // How the things a rubric asks for are named in a question ("Does the team have {name} yet?"). Every concept of CheckpointCriterionCues needs an entry.
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["Roadmap"] = "a development roadmap",
        ["Core features"] = "a list of core features",
        ["Prioritization"] = "a priority order for the features",
        ["User needs"] = "a description of user needs",
        ["Persona"] = "a customer persona",
        ["Target customer"] = "a defined target customer",
        ["Customer journey (CJM)"] = "a customer journey map (CJM)",
        ["Marketing plan"] = "a marketing plan",
        ["Product / demo"] = "a prototype or demo",
        ["UI/UX"] = "a UI/UX design",
        ["Stability"] = "evidence that the product is stable",
        ["Problem"] = "a clearly stated customer problem",
        ["Solution / value"] = "a solution and the value it delivers",
        ["Case requirements"] = "a response to the case requirements",
        ["Launch"] = "a launch plan",
        ["Feedback"] = "user feedback",
        ["Real users"] = "real users",
        ["Sales"] = "sales or orders",
        ["Proof (bills, screenshots)"] = "proof of sales (bills, screenshots)",
        ["Usage data"] = "usage data",
        ["Insights / lessons"] = "lessons learned",
        ["Business model"] = "a business model",
        ["Canvas (BMC)"] = "a Business Model Canvas",
        ["Market size"] = "a market size estimate",
        ["Competitors"] = "a competitor analysis",
        ["Research"] = "research or survey results",
        ["Financials"] = "financial figures",
        ["Team roles"] = "a division of team roles",
        ["Innovation"] = "a point of difference",
        ["Team name"] = "a team name",
        ["Startup idea"] = "a statement of the startup idea",
        ["Startup field"] = "the startup field"
    };

    /// <summary>Concepts that have no name here yet; empty while the two lists agree (a test keeps them so).</summary>
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
            questions.Add($"Does the team have {Names[missing.Label]} yet? This file does not mention it, while the criterion \"{finding.Title}\" asks for it.");
            if (questions.Count == CheckpointSummaryLimits.MaxQuestions) break;
        }
        return questions;
    }
}
