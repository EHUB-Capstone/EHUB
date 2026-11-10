using System.Text;
using System.Text.Json;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Summarization;
using Xunit.Abstractions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Offline evaluation of the checkpoint summary pipeline on the invented submissions of tools/checkpoint-summary/benchmark.
/// It runs the code production runs (text extraction, cleaning, the rule-based quick view and the way files are combined)
/// and compares each requirement's finding with the answer key. No model is involved, so it is deterministic and takes
/// seconds. Unlike the Python benchmark, which lets the model judge requirements, this measures what the backend reports.
///
/// Set EHUB_EVAL_FIXTURES to the folder holding manifest.json (python tools/checkpoint-summary/benchmark/make_fixtures.py
/// writes artifacts/checkpoint-summary/fixtures). The report is written next to it, in ../results/csharp-rules-eval.md.
/// </summary>
public sealed class CheckpointPipelineEvaluation(ITestOutputHelper output)
{
    // The answer key for "NotFound" is strict: a requirement the team did not address must never be shown as related.
    // Baseline measured on F01-F16 (11/2026): none. F13 (a cover page with "Mr. Demo"), F14 (a research goal "xác định MVP") and
    // F16 (a bare title "DEMO") were fixed by CheckpointSummaryText.IsCoverMetadata, IsProductPlan and the one-word rule in
    // CheckpointSummaryEvidence.Find; F05's key was wrong (a prototype click test is a result) and is now Partial.
    // Lower it when the rules improve, never raise it silently.
    private const int AllowedFalseRelated = 0;

    // Requirements the team addressed fully and the rules are expected to find (32 of 32 at the baseline).
    private const int ExpectedCoveredFound = 32;

    [PipelineEvaluationFact]
    public async Task RulesOnly_ReportWhatTheBackendFindsInEachSubmission()
    {
        var root = Environment.GetEnvironmentVariable("EHUB_EVAL_FIXTURES")!;
        var extractor = new SubmissionTextExtractor();
        var topics = new CheckpointSummaryTopics(FileMentorFitRanker.ReadModel(null));
        var settings = new CheckpointSummarySettings();

        var report = new StringBuilder("# Backend rules evaluation\n\n");
        var coveredTotal = 0;
        var coveredFound = 0;
        var notFoundTotal = 0;
        var falseRelated = new List<string>();
        var partialRelated = 0;
        var partialTotal = 0;

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "manifest.json")));
        foreach (var item in manifest.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            var checklist = item.GetProperty("requirements").EnumerateArray()
                .Select((title, index) => new ChecklistItem(index, CheckpointSummarySource.RequirementKind, title.GetString()!, null))
                .ToArray();
            var truth = item.GetProperty("truth").EnumerateArray().Select(value => value.GetString()!).ToArray();

            var perFile = new List<(Guid? FileId, string FileName, IReadOnlyCollection<WorkspaceFileRequirementFinding> Findings)>();
            foreach (var name in item.GetProperty("files").EnumerateArray().Select(value => value.GetString()!))
            {
                var path = Path.Combine(root, id, name);
                var extension = Path.GetExtension(name).ToLowerInvariant();
                var extracted = await extractor.ExtractAsync(await File.ReadAllBytesAsync(path), extension,
                    settings.MaxInputCharacters + 1, CancellationToken.None);
                if (extracted.IsFailure) { report.Append($"- {id} {name}: could not be read ({extracted.Error.Code})\n"); continue; }
                var prepared = CheckpointFileText.Prepare(extracted.Value, extension, settings.MaxInputCharacters);
                if (prepared.IsFailure) { report.Append($"- {id} {name}: no text left after cleaning\n"); continue; }
                var preview = CheckpointSummaryEvidence.Preview(prepared.Value.Text, checklist, topics, prepared.Value.LittleText,
                    prepared.Value.IgnoredInstructions, prepared.Value.Pages);
                perFile.Add((null, name, preview.Requirements.ToArray()));
            }

            var combined = CheckpointSummaryOutput.CombineWithFiles(checklist, perFile);
            report.Append($"## {id} {item.GetProperty("title").GetString()}\n\n| Requirement | Key | Backend | Quote |\n|---|---|---|---|\n");
            for (var i = 0; i < checklist.Length; i++)
            {
                var finding = combined[i];
                var related = finding.Finding == CheckpointSummaryEvidence.Related;
                switch (truth[i])
                {
                    case "Covered": coveredTotal++; if (related) coveredFound++; break;
                    case "NotFound":
                        notFoundTotal++;
                        if (related) falseRelated.Add($"{id}: {checklist[i].Title}");
                        break;
                    default: partialTotal++; if (related) partialRelated++; break;
                }
                var quote = (finding.Evidence ?? string.Empty).Replace('|', '/').Replace('\n', ' ');
                report.Append($"| {checklist[i].Title} | {truth[i]} | {finding.Finding} | {(quote.Length > 110 ? quote[..110] + "…" : quote)} |\n");
            }
            report.Append('\n');
        }

        var summary = $"Covered found: {coveredFound}/{coveredTotal}. Shown as related although not addressed: {falseRelated.Count}/{notFoundTotal}. " +
                      $"Partially addressed shown as related: {partialRelated}/{partialTotal}.";
        report.Insert(0, $"{summary}\n\n");
        if (falseRelated.Count > 0) report.Insert(summary.Length + 2, "False related:\n" + string.Join('\n', falseRelated.Select(line => "- " + line)) + "\n\n");

        var resultsFolder = Path.Combine(Path.GetFullPath(Path.Combine(root, "..")), "results");
        Directory.CreateDirectory(resultsFolder);
        await File.WriteAllTextAsync(Path.Combine(resultsFolder, "csharp-rules-eval.md"), report.ToString(), Encoding.UTF8);
        output.WriteLine(summary);
        foreach (var line in falseRelated) output.WriteLine("false related: " + line);

        Assert.True(falseRelated.Count <= AllowedFalseRelated, summary + "\n" + string.Join('\n', falseRelated));
        Assert.True(coveredFound >= ExpectedCoveredFound, summary);
    }
}

/// <summary>Runs only when EHUB_EVAL_FIXTURES points at the generated sample submissions.</summary>
public sealed class PipelineEvaluationFactAttribute : FactAttribute
{
    public PipelineEvaluationFactAttribute()
    {
        var folder = Environment.GetEnvironmentVariable("EHUB_EVAL_FIXTURES");
        if (string.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder, "manifest.json")))
            Skip = "Set EHUB_EVAL_FIXTURES to the folder with manifest.json (see tools/checkpoint-summary/benchmark/README.md).";
    }
}
