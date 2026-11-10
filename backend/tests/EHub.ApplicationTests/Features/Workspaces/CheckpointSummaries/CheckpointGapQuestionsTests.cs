using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointGapQuestionsTests
{
    private static WorkspaceCriterionCue Cue(string label, bool found) => new() { Label = label, Found = found };

    private static WorkspaceFileRequirementFinding Criterion(
        int index, string title, decimal weight, string finding, params WorkspaceCriterionCue[] cues) => new()
    {
        Index = index, Kind = CheckpointSummarySource.CriterionKind, Title = title, Finding = finding, Weight = weight,
        Assessment = CheckpointCriterionProfile.Document, Cues = cues
    };

    [Fact]
    public void EveryConceptTheCuesKnowHasAVietnameseName()
    {
        CheckpointGapQuestions.UnnamedConcepts().Should().BeEmpty();
    }

    [Fact]
    public void AskAboutTheThingACoveredCriterionExpectsAndTheFileLeavesOut()
    {
        var findings = new[]
        {
            Criterion(0, "Persona & CJM", 20, CheckpointSummaryEvidence.Related, Cue("Persona", true), Cue("Customer journey (CJM)", false))
        };

        var questions = CheckpointGapQuestions.From(findings, littleText: false);

        questions.Should().ContainSingle().Which.Should().Be(
            "Nhóm đã có hành trình khách hàng (CJM) chưa? File này chưa nhắc tới, trong khi tiêu chí \"Persona & CJM\" cần phần này.");
    }

    [Fact]
    public void AskNothingWhenTheFileShowsNoneOfWhatTheCriterionExpects()
    {
        // "Not found" already says so; a question for every missing thing would only repeat it.
        var findings = new[] { Criterion(0, "Roadmap", 20, CheckpointSummaryEvidence.Related, Cue("Roadmap", false), Cue("Prioritization", false)) };

        CheckpointGapQuestions.From(findings, littleText: false).Should().BeEmpty();
    }

    [Fact]
    public void AskNothingWhenEveryExpectedThingIsMentioned()
    {
        var findings = new[] { Criterion(0, "Roadmap", 20, CheckpointSummaryEvidence.Related, Cue("Roadmap", true), Cue("Prioritization", true)) };

        CheckpointGapQuestions.From(findings, littleText: false).Should().BeEmpty();
    }

    [Fact]
    public void AskNothingAboutAFileThatIsMostlyImages()
    {
        var findings = new[] { Criterion(0, "Roadmap", 20, CheckpointSummaryEvidence.Related, Cue("Roadmap", true), Cue("Prioritization", false)) };

        CheckpointGapQuestions.From(findings, littleText: true).Should().BeEmpty();
    }

    [Fact]
    public void KeepToTwoQuestionsHeaviestCriterionFirstAndAskEachThingOnce()
    {
        var findings = new[]
        {
            Criterion(0, "Light", 5, CheckpointSummaryEvidence.Related, Cue("Persona", true), Cue("Feedback", false)),
            Criterion(1, "Heavy", 40, CheckpointSummaryEvidence.Related, Cue("Roadmap", true), Cue("Prioritization", false)),
            Criterion(2, "Medium", 20, CheckpointSummaryEvidence.Related, Cue("Persona", true), Cue("Prioritization", false), Cue("Sales", false)),
            Criterion(3, "Last", 10, CheckpointSummaryEvidence.Related, Cue("Persona", true), Cue("Launch", false))
        };

        var questions = CheckpointGapQuestions.From(findings, littleText: false);

        questions.Should().HaveCount(2);
        questions[0].Should().Contain("thứ tự ưu tiên tính năng").And.Contain("\"Heavy\"");
        // Prioritization was already asked, so the next criterion contributes its next missing thing.
        questions[1].Should().Contain("doanh thu hoặc đơn hàng").And.Contain("\"Medium\"");
    }

    [Fact]
    public void SkipCriteriaThatAreNotReadableFromAFile()
    {
        var live = Criterion(0, "Presentation", 20, CheckpointSummaryEvidence.Live, Cue("Persona", true), Cue("Feedback", false));
        var notFound = Criterion(1, "Roadmap", 20, CheckpointSummaryEvidence.NotFound, Cue("Roadmap", true), Cue("Prioritization", false));
        var requirement = new WorkspaceFileRequirementFinding
        {
            Index = 2, Kind = CheckpointSummarySource.RequirementKind, Title = "Kế hoạch", Finding = CheckpointSummaryEvidence.Related,
            Assessment = CheckpointCriterionProfile.Document, Cues = [Cue("Roadmap", true), Cue("Launch", false)]
        };

        CheckpointGapQuestions.From([live, notFound, requirement], littleText: false).Should().BeEmpty();
    }

    [Fact]
    public void TheQuestionComesBeforeTheModelsQuestionsAndTheTotalStaysWithinTheLimit()
    {
        var preview = new WorkspaceFilePreview
        {
            Requirements = [Criterion(0, "Persona & CJM", 20, CheckpointSummaryEvidence.Related, Cue("Persona", true), Cue("Customer journey (CJM)", false))]
        };
        const string answer = """
            {"overview":"Tài liệu mô tả chân dung khách hàng.","keyPoints":[],"questions":["Nhóm sẽ thu thập thêm phản hồi từ khách hàng như thế nào?","Nhóm có kế hoạch gì cho tháng sau?","Nhóm dự kiến ai phụ trách?"]}
            """;

        var content = CheckpointSummaryOutput.TryBuild(answer, preview);

        content!.Questions.Should().HaveCount(2);
        content.Questions.First().Should().Contain("hành trình khách hàng (CJM)");
    }
}
