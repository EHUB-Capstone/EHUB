using System.Text.Json;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Validates the model's short answer for one file and combines it with the backend's quick view (key sentences and
/// checklist evidence). The model is not trusted: lengths are clipped and it never writes quotes.
/// </summary>
internal static class CheckpointSummaryOutput
{
    public const string NotChecked = "NotChecked";

    /// <param name="grounding">
    /// When given, key points and later overview sentences that the file's words do not back are dropped. Questions
    /// are about what the file lacks, so they cannot be checked word by word; only those built on a misread figure (a
    /// market size taken as a goal), a figure the file never gives, or a contrast it never makes are dropped. A file that is mostly images gets no questions: the model saw only
    /// titles, so its questions would be guesses.
    /// </param>
    public static WorkspaceFileSummaryContent? TryBuild(string json, WorkspaceFilePreview preview, CheckpointSummaryGrounding? grounding = null)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("overview", out var overview) ||
                overview.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(overview.GetString()))
                return null;
            var overviewText = CheckpointSummaryText.Clean(overview.GetString());
            if (grounding is not null) overviewText = grounding.GroundedOverview(overviewText);
            var keyPoints = Clean(Strings(root, "keyPoints"), CheckpointSummaryLimits.MaxKeyPoints);
            if (grounding is not null) keyPoints = keyPoints.Where(grounding.Supports).ToArray();
            // The status list and the conflict list are worked out from the file itself, so they win over the model: a
            // point calling pending work finished is dropped, and a point stating one side of a conflict says so.
            keyPoints = keyPoints
                .Where(point => !CheckpointStatusLines.Contradicts(point, preview.StatusItems))
                .Select(point => CheckpointFigureConflicts.Annotate(point, preview.Conflicts))
                .ToArray();
            string[] questions = preview.LittleText ? []
                : Clean(Strings(root, "questions"), CheckpointSummaryLimits.MaxQuestions * 2)
                    .Select(CheckpointSummaryText.AsTeamQuestion).OfType<string>().Take(CheckpointSummaryLimits.MaxQuestions).ToArray();
            if (grounding is not null)
                questions = questions.Where(question => !grounding.MisusesMarketSize(question) && !grounding.AssumesUnseenFact(question)).ToArray();
            return new WorkspaceFileSummaryContent
            {
                Overview = CheckpointSummaryText.Clip(overviewText, CheckpointSummaryLimits.MaxOverviewCharacters),
                KeyPoints = keyPoints,
                Questions = questions,
                Highlights = preview.Highlights,
                Requirements = preview.Requirements,
                Conflicts = preview.Conflicts,
                StatusItems = preview.StatusItems,
                Figures = preview.Figures,
                IgnoredInstructions = preview.IgnoredInstructions,
                LittleText = preview.LittleText
            };
        }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Combines the per-file findings, newest file first: an item is Related if any file read so far quotes it (with
    /// that file's name), NotFound if files were read but none does, NotChecked when no file has been read yet. Items
    /// judged live stay Live, and product items no file mentions stay Product. A finding is matched by position and by
    /// title: a summary made before the rubric was edited must not lend its evidence to a different criterion.
    /// </summary>
    public static IReadOnlyList<WorkspaceCheckpointRequirementFinding> Combine(
        IReadOnlyList<ChecklistItem> checklist,
        IReadOnlyList<(string FileName, IReadOnlyCollection<WorkspaceFileRequirementFinding> Findings)> files)
    {
        return checklist.Select(item =>
        {
            // A file whose finding at this position has another title was summarised against an older rubric: it says
            // nothing about this criterion, so it is skipped.
            var applicable = files.Where(file => !file.Findings.Any(finding =>
                finding.Index == item.Index && !string.Equals(finding.Title, item.Title, StringComparison.Ordinal))).ToArray();
            var matches = applicable
                .Select(file => (file.FileName, Finding: file.Findings.FirstOrDefault(finding => finding.Index == item.Index)))
                .Where(match => match.Finding is not null)
                .ToArray();
            // The file that mentions most of what the criterion expects is quoted; files are listed newest first, so
            // ties go to the newest.
            var related = matches.Where(match => match.Finding!.Finding == CheckpointSummaryEvidence.Related)
                .OrderByDescending(match => match.Finding!.Evidence is not null)
                .ThenByDescending(match => match.Finding!.Cues.Count(cue => cue.Found))
                .FirstOrDefault();
            var read = applicable.Length > 0;
            string finding;
            if (item.Assessment == CheckpointCriterionProfile.Live) finding = CheckpointSummaryEvidence.Live;
            else if (related.Finding is not null) finding = CheckpointSummaryEvidence.Related;
            else if (!read) finding = NotChecked;
            else finding = CheckpointSummaryEvidence.MissingFinding(item.Assessment);
            return new WorkspaceCheckpointRequirementFinding
            {
                Index = item.Index,
                Kind = item.Kind,
                Title = item.Title,
                Finding = finding,
                Evidence = related.Finding?.Evidence,
                Source = related.Finding is null ? null : related.FileName,
                Weight = item.Weight,
                Assessment = item.Assessment,
                Cues = MergeCues(matches.Select(match => match.Finding!.Cues))
            };
        }).ToArray();
    }

    /// <summary>A cue counts as found when any file mentions it; the first file's sentence is shown.</summary>
    private static IReadOnlyCollection<WorkspaceCriterionCue> MergeCues(IEnumerable<IReadOnlyCollection<WorkspaceCriterionCue>> perFile) =>
        perFile.SelectMany(cues => cues)
            .GroupBy(cue => cue.Label)
            .Select(group => group.FirstOrDefault(cue => cue.Found) ?? group.First())
            .ToArray();

    public static string Domain(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;

    private static string[] Clean(IEnumerable<string> values, int max) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => CheckpointSummaryText.Clip(CheckpointSummaryText.Clean(value), CheckpointSummaryLimits.MaxItemCharacters))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(max)
            .ToArray();

    private static IEnumerable<string> Strings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)
            : [];
}
