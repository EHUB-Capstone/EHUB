using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointRubricAwarenessTests
{
    [Theory]
    // EXE201 rubric
    [InlineData("Product Roadmap & Core Features", "Document")]
    [InlineData("Product Completeness (UI/UX & Stability)/ MVP demo", "Product")]
    [InlineData("Problem Solving & Practicality (Persona & CJM) + Marketing Plan", "Document")]
    [InlineData("Demo Skills & Q&A", "Live")]
    [InlineData("Case Execution", "Document")]
    [InlineData("Launching & Feedback Collection", "Document")]
    [InlineData("Presentation Skills & Teamwork", "Live")]
    [InlineData("Product Understanding & Q&A", "Live")]
    [InlineData("Market Validation Report & Evidence", "Document")]
    [InlineData("Product Evaluation", "Document")]
    [InlineData("Data Analysis (Insights & Lessons)", "Document")]
    [InlineData("Final Presentation", "Live")]
    [InlineData("Progress & Effort", "Live")]
    // EXE101 rubric
    [InlineData("Startup Idea Clarity", "Overall")]
    [InlineData("Team Roles & Contribution", "Document")]
    [InlineData("Presentation Quality", "Live")]
    [InlineData("Q&A / Discussion", "Live")]
    [InlineData("Problem–Solution Fit", "Document")]
    [InlineData("MVP / Prototype Quality", "Product")]
    [InlineData("Business Model Canvas", "Document")]
    [InlineData("Presentation & Demo", "Live")]
    [InlineData("Product Output", "Product")]
    [InlineData("Startup Completeness", "Overall")]
    [InlineData("Pitch & Storytelling", "Live")]
    [InlineData("Q&A & Defense", "Live")]
    public void Classify_SeparatesWhatAFileCanShowFromWhatIsJudgedLiveOrOnTheProduct(string title, string expected)
    {
        CheckpointCriterionProfile.Classify(title).Should().Be(expected);
    }

    [Fact]
    public void ExcellentText_ReadsTheImportedLevelsAndFallsBackToTheFirstOne()
    {
        const string levels = """
            [{"key":"excellent","label":"Excellent","range":"8.5–10","description":"Clear, realistic roadmap; well-prioritized core features"},
             {"key":"good","label":"Good","range":"7.0–8.4","description":"Clear roadmap but limited depth"}]
            """;

        CheckpointCriterionProfile.ExcellentText(levels).Should().Be("Clear, realistic roadmap; well-prioritized core features");
        CheckpointCriterionProfile.ExcellentText("""[{"key":"a","description":"First level"},{"key":"b","description":"Second"}]""")
            .Should().Be("First level");
        CheckpointCriterionProfile.ExcellentText("[]").Should().BeNull();
        CheckpointCriterionProfile.ExcellentText("not json").Should().BeNull();
        CheckpointCriterionProfile.ExcellentText(null).Should().BeNull();
    }

    [Fact]
    public void Cues_NameWhatTheExcellentLevelExpectsAndCheckWhichOfItAFileMentions()
    {
        const string excellent = "Clear, realistic roadmap; well-prioritized core features linked to user needs";
        string[] file =
        [
            "PRODUCT ROADMAP",
            "Milestone Q3/2026: complete MVP version 2 and onboard 300 beta users.",
            "Core features: format preservation, LaTeX and Mermaid rendering."
        ];
        var normalised = file.Select(line => EHub.Application.Features.Mentoring.TrainedRanking.MentorRankingFeatures.Normalize(line)).ToArray();

        // In the order the Excellent text mentions them ("well-prioritized core features").
        CheckpointCriterionCues.Wanted(null, excellent).Should().Equal("Roadmap", "Prioritization", "Core features", "User needs");
        var cues = CheckpointCriterionCues.Find(null, excellent, file, normalised);

        cues.Select(cue => (cue.Label, cue.Found)).Should().Equal(
            ("Roadmap", true), ("Prioritization", false), ("Core features", true), ("User needs", false));
        cues[0].Evidence.Should().StartWith("Milestone Q3/2026");
    }

    [Fact]
    public void Cues_UnderstandVietnameseFiles()
    {
        string[] file = ["Lộ trình phát triển: quý 3 hoàn thành MVP.", "Chân dung khách hàng: sinh viên năm hai.", "Hành trình khách hàng gồm 5 bước."];
        var normalised = file.Select(line => EHub.Application.Features.Mentoring.TrainedRanking.MentorRankingFeatures.Normalize(line)).ToArray();

        var cues = CheckpointCriterionCues.Find(null, "Strong problem-solution fit supported by clear persona and CJM", file, normalised);

        cues.Where(cue => cue.Label is "Persona" or "Customer journey (CJM)").Should().OnlyContain(cue => cue.Found);
    }

    [Fact]
    public void Find_DoesNotSearchLiveCriteriaAndMarksUnmentionedProductCriteria()
    {
        ChecklistItem[] checklist =
        [
            new(0, "Criterion", "Product Roadmap & Core Features", "Clear roadmap and core features", 30, "Document", "Clear roadmap and core features"),
            new(1, "Criterion", "Product Completeness (UI/UX & Stability) / MVP demo", "Stable usable product", 25, "Product", "Stable usable product"),
            new(2, "Criterion", "Demo Skills & Q&A", "Smooth demo", 15, "Live", "Smooth demo")
        ];
        const string text = "Roadmap: Q3/2026 complete the app.\nCore features: formatting and diagrams.\nOur team answered every question in the demo and Q&A.";

        var findings = CheckpointSummaryEvidence.Find(text, checklist, CheckpointSummaryTopics.None);

        findings[0].Finding.Should().Be("Related");
        findings[0].Weight.Should().Be(30);
        // The sentence about the demo and Q&A does not make the live criterion "found": no file can show it.
        findings[2].Finding.Should().Be("Live");
        findings[2].Evidence.Should().BeNull();
        findings[2].Cues.Should().BeEmpty();
        findings[1].Assessment.Should().Be("Product");
    }

    [Fact]
    public void Combine_KeepsLiveAndProductItemsApartAndIgnoresFindingsOfAnEditedRubric()
    {
        ChecklistItem[] checklist =
        [
            new(0, "Criterion", "Product Roadmap", null, 60, "Document"),
            new(1, "Criterion", "Product Completeness / MVP demo", null, 25, "Product"),
            new(2, "Criterion", "Demo Skills & Q&A", null, 15, "Live")
        ];
        WorkspaceFileRequirementFinding Old(int index, string title, string finding) =>
            new() { Index = index, Kind = "Criterion", Title = title, Finding = finding, Evidence = finding == "Related" ? "A quote" : null };
        // Made before the rubric was edited: index 0 was another criterion then.
        var stale = new[] { Old(0, "Problem Clarity", "Related"), Old(1, "Product Completeness / MVP demo", "NotFound") };

        var combined = CheckpointSummaryOutput.Combine(checklist, [("old.pdf", stale)]);

        combined[0].Finding.Should().Be("NotChecked", "the stale finding belongs to a criterion that no longer has this title");
        combined[1].Finding.Should().Be("Product");
        combined[2].Finding.Should().Be("Live");
        combined.Select(item => item.Weight).Should().Equal(60m, 25m, 15m);
    }

    [Fact]
    public void Prompt_ListsOnlyWhatDocumentsCanShowHeaviestFirstWithWeights()
    {
        ChecklistItem[] checklist =
        [
            new(0, "Criterion", "Product Roadmap", "Clear roadmap", 30, "Document"),
            new(1, "Criterion", "Demo Skills & Q&A", "Smooth demo", 15, "Live"),
            new(2, "Criterion", "Market Validation Report", "Sales report with evidence", 40, "Document")
        ];

        var prompt = CheckpointSummaryPrompt.User(1, "Checkpoint", checklist, "deck.pdf", "Nội dung", false, followChecklist: true);

        prompt.Should().Contain("- Market Validation Report (40%): Sales report with evidence")
            .And.Contain("- Product Roadmap (30%): Clear roadmap")
            .And.NotContain("Demo Skills");
        prompt.IndexOf("Market Validation Report", StringComparison.Ordinal)
            .Should().BeLessThan(prompt.IndexOf("Product Roadmap", StringComparison.Ordinal));
        CheckpointSummaryEvidence.ChecklistTerms(checklist).Should().NotContain(term => term.Contains("Demo Skills"));
    }

    [Fact]
    public async Task Checklist_UsesTheExcellentLevelWhenAnImportedCriterionHasNoDescription()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var course = new Course { Code = "EXE201", Name = "Startup 2" };
        var checkpoint = new Checkpoint { Course = course, Name = "Checkpoint 1", CheckpointNumber = 1 };
        var rubric = new Rubric { Course = course, Checkpoint = checkpoint, Name = "EXE201 Checkpoint 1", TotalWeight = 100 };
        db.RubricCriteria.AddRange(
            new RubricCriterion
            {
                Rubric = rubric, Key = "roadmap", Name = "Product Roadmap & Core Features", Weight = 30, DisplayOrder = 1,
                LevelsJson = """[{"key":"excellent","label":"Excellent","range":"8.5–10","description":"Clear, realistic roadmap"}]"""
            },
            new RubricCriterion { Rubric = rubric, Key = "demo", Name = "Demo Skills & Q&A", Weight = 15, DisplayOrder = 2, Description = "Written description" });
        await db.SaveChangesAsync();

        var checklist = await CheckpointSummarySource.ChecklistAsync(db, checkpoint, default);

        checklist[0].Description.Should().Be("Clear, realistic roadmap");
        checklist[0].Excellent.Should().Be("Clear, realistic roadmap");
        checklist[0].Weight.Should().Be(30);
        checklist[0].Assessment.Should().Be("Document");
        checklist[1].Description.Should().Be("Written description", "an explicit description wins over the level text");
        checklist[1].Assessment.Should().Be("Live");
    }
}
