using EHub.Contracts.Workspaces;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Shows how a team moves from one checkpoint to the next: for each thing a rubric asks for (the concepts of
/// <see cref="CheckpointCriterionCues"/>: roadmap, persona, market size...), whether the files of each checkpoint mention it.
/// Rubrics differ between checkpoints, so the concept is the stable key, not the criterion title. It only reads what the quick
/// view and the summary already stored; there is no model, no file download and no verdict: "mentioned" says a file names the
/// thing, not that it is done well, and "not mentioned" only speaks for the files that were read.
/// </summary>
internal static class CheckpointCoverageProgress
{
    public const string Found = "Found";
    public const string NotMentioned = "NotMentioned";
    public const string NotAsked = "NotAsked";
    public const string NotRead = "NotRead";

    public const string Gained = "Gained";
    public const string Dropped = "Dropped";
    public const string Held = "Held";
    public const string Missing = "Missing";
    public const string None = "None";

    /// <summary>Enough to read at a glance; the ones that changed come first, so the cap drops the unremarkable.</summary>
    public const int MaximumConcepts = 12;

    /// <param name="Number">The checkpoint number.</param>
    /// <param name="FileCount">Files the team has in the checkpoint (newest version of each).</param>
    /// <param name="ReadFiles">The stored requirement findings of each file whose text was read.</param>
    internal sealed record CheckpointInput(int Number, int FileCount, IReadOnlyList<IReadOnlyCollection<WorkspaceFileRequirementFinding>> ReadFiles);

    public static WorkspaceCheckpointProgressResponse Build(int currentNumber, IEnumerable<CheckpointInput> checkpoints)
    {
        var ordered = checkpoints.Where(item => item.FileCount > 0 && item.Number <= currentNumber).OrderBy(item => item.Number).ToArray();
        // One checkpoint alone has nothing to be compared with, and a current checkpoint without files has nothing to say.
        if (ordered.Length < 2 || ordered[^1].Number != currentNumber)
            return new WorkspaceCheckpointProgressResponse { CheckpointNumber = currentNumber };

        var asked = new Dictionary<int, HashSet<string>>();
        var found = new Dictionary<int, HashSet<string>>();
        foreach (var checkpoint in ordered)
        {
            var cues = checkpoint.ReadFiles.SelectMany(file => file).SelectMany(finding => finding.Cues).ToArray();
            asked[checkpoint.Number] = cues.Select(cue => cue.Label).ToHashSet(StringComparer.Ordinal);
            found[checkpoint.Number] = cues.Where(cue => cue.Found).Select(cue => cue.Label).ToHashSet(StringComparer.Ordinal);
        }

        string State(CheckpointInput checkpoint, string label) =>
            checkpoint.ReadFiles.Count == 0 ? NotRead
            : found[checkpoint.Number].Contains(label) ? Found
            : asked[checkpoint.Number].Contains(label) ? NotMentioned
            : NotAsked;

        var labels = ordered.SelectMany(checkpoint => asked[checkpoint.Number]).Distinct(StringComparer.Ordinal).ToArray();
        var concepts = labels.Select(label =>
        {
            var cells = ordered.Select(checkpoint => (checkpoint.Number, State: State(checkpoint, label))).ToArray();
            return new WorkspaceProgressConcept
            {
                Label = label,
                Change = ChangeOf(cells),
                Cells = cells.Select(cell => new WorkspaceProgressCell { CheckpointNumber = cell.Number, State = cell.State }).ToArray()
            };
        })
        // Worth a look first (no longer mentioned, still missing), then what improved, then what held.
        .OrderBy(concept => Rank(concept.Change)).ThenBy(concept => concept.Label, StringComparer.Ordinal)
        .Take(MaximumConcepts)
        .ToArray();

        return new WorkspaceCheckpointProgressResponse
        {
            CheckpointNumber = currentNumber,
            Checkpoints = ordered.Select(item => new WorkspaceProgressCheckpoint { Number = item.Number, FileCount = item.FileCount, ReadFileCount = item.ReadFiles.Count }).ToArray(),
            Concepts = concepts
        };
    }

    /// <summary>The newest checkpoint against the nearest earlier one that could tell (that asked for the thing and had files read).</summary>
    private static string ChangeOf(IReadOnlyList<(int Number, string State)> cells)
    {
        var current = cells[^1].State;
        if (current is not (Found or NotMentioned)) return None;
        var earlier = cells.Take(cells.Count - 1).LastOrDefault(cell => cell.State is Found or NotMentioned);
        if (earlier.State is null) return None;
        return (current, earlier.State) switch
        {
            (Found, Found) => Held,
            (Found, _) => Gained,
            (_, Found) => Dropped,
            _ => Missing
        };
    }

    private static int Rank(string change) => change switch { Dropped => 0, Missing => 1, Gained => 2, Held => 3, _ => 4 };
}
