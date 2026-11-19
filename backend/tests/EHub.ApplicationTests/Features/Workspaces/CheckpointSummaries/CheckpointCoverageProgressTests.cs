using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointCoverageProgressTests
{
    private static WorkspaceCriterionCue Cue(string label, bool found) => new() { Label = label, Found = found };

    private static IReadOnlyCollection<WorkspaceFileRequirementFinding> File(params WorkspaceCriterionCue[] cues) =>
        [new WorkspaceFileRequirementFinding { Index = 0, Kind = "Criterion", Title = "Criterion", Finding = "Related", Cues = cues }];

    private static CheckpointCoverageProgress.CheckpointInput Checkpoint(int number, int files, params IReadOnlyCollection<WorkspaceFileRequirementFinding>[] read) =>
        new(number, files, read);

    private static WorkspaceProgressConcept Concept(WorkspaceCheckpointProgressResponse response, string label) =>
        response.Concepts.Single(item => item.Label == label);

    [Fact]
    public void EachThingIsComparedWithTheNearestEarlierCheckpointThatCouldTell()
    {
        var response = CheckpointCoverageProgress.Build(3,
        [
            Checkpoint(1, 1, File(Cue("Persona", false), Cue("Roadmap", true), Cue("Competitors", false), Cue("Sales", true))),
            Checkpoint(2, 1, File(Cue("Persona", true), Cue("Roadmap", true), Cue("Competitors", false))),
            Checkpoint(3, 1, File(Cue("Persona", true), Cue("Roadmap", false), Cue("Competitors", false), Cue("Sales", true)))
        ]);

        Concept(response, "Persona").Change.Should().Be(CheckpointCoverageProgress.Held);
        Concept(response, "Roadmap").Change.Should().Be(CheckpointCoverageProgress.Dropped);
        Concept(response, "Competitors").Change.Should().Be(CheckpointCoverageProgress.Missing);
        // Sales was not asked in checkpoint 2, so checkpoint 3 is compared with checkpoint 1.
        var sales = Concept(response, "Sales");
        sales.Change.Should().Be(CheckpointCoverageProgress.Held);
        sales.Cells.Select(cell => cell.State).Should().Equal(
            CheckpointCoverageProgress.Found, CheckpointCoverageProgress.NotAsked, CheckpointCoverageProgress.Found);
    }

    [Fact]
    public void AThingMentionedForTheFirstTimeIsGained()
    {
        var response = CheckpointCoverageProgress.Build(2,
        [
            Checkpoint(1, 1, File(Cue("Persona", false))),
            Checkpoint(2, 1, File(Cue("Persona", true)))
        ]);

        Concept(response, "Persona").Change.Should().Be(CheckpointCoverageProgress.Gained);
    }

    [Fact]
    public void ChangesThatNeedALookComeFirstAndTheListIsCapped()
    {
        var labels = Enumerable.Range(0, 15).Select(index => $"Held-{index:00}").ToArray();
        var response = CheckpointCoverageProgress.Build(2,
        [
            Checkpoint(1, 1, File([.. labels.Select(label => Cue(label, true)), Cue("Zeta", true)])),
            Checkpoint(2, 1, File([.. labels.Select(label => Cue(label, true)), Cue("Zeta", false)]))
        ]);

        response.Concepts.Should().HaveCount(CheckpointCoverageProgress.MaximumConcepts);
        response.Concepts.First().Label.Should().Be("Zeta");
        response.Concepts.First().Change.Should().Be(CheckpointCoverageProgress.Dropped);
    }

    [Fact]
    public void AFileNobodyReadIsNeverReportedAsNotMentioned()
    {
        var response = CheckpointCoverageProgress.Build(2,
        [
            Checkpoint(1, 2),
            Checkpoint(2, 1, File(Cue("Persona", false)))
        ]);

        var persona = Concept(response, "Persona");
        persona.Cells.Select(cell => cell.State).Should().Equal(CheckpointCoverageProgress.NotRead, CheckpointCoverageProgress.NotMentioned);
        persona.Change.Should().Be(CheckpointCoverageProgress.None, "checkpoint 1 could not tell");
        response.Checkpoints.Select(item => (item.Number, item.FileCount, item.ReadFileCount)).Should().Equal((1, 2, 0), (2, 1, 1));
    }

    [Fact]
    public void NothingIsComparedWithoutTwoCheckpointsWithFilesAndTheCurrentOneIncluded()
    {
        var one = CheckpointCoverageProgress.Build(1, [Checkpoint(1, 1, File(Cue("Persona", true)))]);
        one.Checkpoints.Should().BeEmpty();
        one.Concepts.Should().BeEmpty();

        var noCurrentFiles = CheckpointCoverageProgress.Build(3,
            [Checkpoint(1, 1, File(Cue("Persona", true))), Checkpoint(2, 1, File(Cue("Persona", true))), Checkpoint(3, 0)]);
        noCurrentFiles.Concepts.Should().BeEmpty();

        // A later checkpoint is not part of the picture of an earlier one.
        var later = CheckpointCoverageProgress.Build(2,
            [Checkpoint(1, 1, File(Cue("Persona", false))), Checkpoint(2, 1, File(Cue("Persona", true))), Checkpoint(3, 1, File(Cue("Persona", false)))]);
        later.Checkpoints.Select(item => item.Number).Should().Equal(1, 2);
    }

    [Fact]
    public void NothingIsShownForConceptsNoRubricAskedFor()
    {
        var response = CheckpointCoverageProgress.Build(2,
            [Checkpoint(1, 1, File()), Checkpoint(2, 1, File())]);

        response.Checkpoints.Should().HaveCount(2);
        response.Concepts.Should().BeEmpty();
    }
}
