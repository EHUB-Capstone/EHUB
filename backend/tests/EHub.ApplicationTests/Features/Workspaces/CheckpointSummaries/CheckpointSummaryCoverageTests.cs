using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointSummaryCoverageTests
{
    private static readonly ChecklistItem[] Rubric =
    [
        new(0, "Criterion", "Problem Clarity", "Clearly defined problem statement, customer pain points, and target persona."),
        new(1, "Criterion", "Market Potential", "Identified market size, potential competitors, and competitive advantage."),
        new(2, "Criterion", "Business Model", "Revenue model, pricing strategy, and initial cost assumptions are logical.")
    ];

    // A pitch deck in English whose financial slides are full of figures, as in a real Checkpoint 1 submission.
    private static readonly string Deck = string.Join('\n',
        "KEY CUSTOMER PAIN",
        "Students frequently copy content from AI tools and websites into Word documents.",
        "Copy → Formatting errors → Manual correction → Time wasted",
        "OUR SOLUTION",
        "The desktop app preserves headings, tables and formulas and standardises the layout automatically.",
        "MARKET SIZE",
        "Estimated market of 2 million university students and academic users in Vietnam.",
        "REVENUE MODEL",
        "Pro plan pricing 39K VND/month unlocks unlimited processing and advanced features.",
        "FINANCIAL PLAN",
        string.Join('\n', Enumerable.Range(1, 60).Select(i => $"Metric {i} Year 1 {i * 100} Year 2 {i * 200} Total {i * 300}")),
        "Total Users 2,000 4,000 6,000",
        "280 Million");

    [Fact]
    public void Select_GivesEverySectionASentenceBeforeFillingWithFigures()
    {
        var selected = CheckpointSummaryText.Select(Deck, CheckpointSummaryEvidence.ChecklistTerms(Rubric), 900);

        selected.Length.Should().BeLessThanOrEqualTo(900);
        // The problem and solution are kept although the financial table has far more figures.
        selected.Should().Contain("preserves headings").And.Contain("2 million university students").And.Contain("Pro plan pricing 39K");
        selected.Should().Match(text => text.Contains("Students frequently copy content") || text.Contains("Copy → Formatting errors"));
    }

    [Theory]
    [InlineData("KEY CUSTOMER PAIN", true)]
    [InlineData("Point 1:", true)]
    [InlineData("REVENUE MODEL UNIT ECONOMICS", true)]
    [InlineData("Students & Working Professionals", false)]
    [InlineData("Estimated at 2 million university students.", false)]
    public void IsTitle_RecognisesSlideTitlesAndLabels(string line, bool expected)
    {
        CheckpointSummaryText.IsTitle(line).Should().Be(expected);
    }

    [Fact]
    public void Find_UsesTheCriterionDescriptionAndNeverQuotesATitle()
    {
        var findings = CheckpointSummaryEvidence.Find(Deck, Rubric, CheckpointSummaryTopics.None);

        findings.Should().OnlyContain(item => item.Finding == CheckpointSummaryEvidence.Related);
        // "customer pain" (from the description) matches the title; the content line next to it is quoted.
        findings[0].Evidence.Should().StartWith("Students frequently copy content");
        findings[1].Evidence.Should().StartWith("Estimated market of 2 million");
        findings[2].Evidence.Should().StartWith("Pro plan pricing 39K");
        findings.Should().NotContain(item => item.Evidence == "KEY CUSTOMER PAIN" || item.Evidence == "REVENUE MODEL");
    }

    [Fact]
    public void Conflicts_ReportFiguresStatedWithDifferentValues()
    {
        const string text =
            "750 Premium Subscribers (15% conversion rate)\n" +
            "Premium Users 600 (Year 1) 30% conversion target\n" +
            "30% OF USERS UPGRADE TO PREMIUM.\n" +
            "ARPU 39K VND/month → Gross Margin ~85%\n" +
            "LTV ~412,500 VND (based on 8% monthly churn)\n" +
            "D30 retention D30 ≥15% Ask for reasons behind churn, fix the app\n" +
            "Team Expansion 20% 100 Million VND\n" +
            "10% (50 Million VND): Team Expansion\n" +
            "Product Development 40% 200 Million VND\n" +
            "40% (200 Million VND): Product Development & AI";

        var conflicts = CheckpointFigureConflicts.Find(text);

        conflicts.Select(item => item.Subject).Should().Equal("Conversion rate", "Budget share: Team Expansion");
        conflicts[0].Values.Select(item => item.Value).Should().Equal("15%", "30%");
        conflicts[0].Values.First().Quote.Should().Be("750 Premium Subscribers (15% conversion rate)");
        conflicts[1].Values.Select(item => item.Value).Should().Equal("20%", "10%");
    }

    [Fact]
    public void Conflicts_IgnoreFiguresThatDifferByYear()
    {
        const string text =
            "Năm 1 doanh thu tăng 20% đạt 100 triệu đồng\n" +
            "Năm 2 doanh thu tăng 30% đạt 200 triệu đồng\n" +
            "Tỷ lệ chuyển đổi 5% trong tháng đầu";

        CheckpointFigureConflicts.Find(text).Should().BeEmpty();
    }

    [Fact]
    public void Prompt_ShowsCriterionDescriptionsAndAsksToCoverThem()
    {
        var prompt = CheckpointSummaryPrompt.User(1, "Idea", Rubric, "deck.pdf", "Nội dung", excerpt: false, followChecklist: true);

        CheckpointSummaryPrompt.Version.Should().Be("v12");
        prompt.Should().Contain("- Problem Clarity: Clearly defined problem statement")
            .And.Contain("mỗi ý về một mục khác nhau trong danh sách cần xem")
            .And.Contain("không ghép số của các phần khác nhau");
    }
}
