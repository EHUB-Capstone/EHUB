using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Problems found in the summary of a short introduction deck (J4U, EXE101 Checkpoint 1): the slides say "Project Name" and
/// "Group Name", "Business Area" and "Startup Ideas", while the checklist asks for "Team Name", "Startup Field" and
/// "Startup Idea". The text was read in full; only the words differed, and the items were reported as missing.
/// </summary>
public sealed class CheckpointIdentityDeckFindingsTests
{
    private const string Deck =
        "J4U\n" +
        "Introduction to the Project\n" +
        "J4U\n" +
        "Introduction\n" +
        "Project Name: J4U – Jobs for You\n" +
        "A platform connecting students with short-term jobs and a dedicated community\n" +
        "Group Name: J4U\n" +
        "Problem Statement\n" +
        "There is a huge demand for short-term student work.\n" +
        "Currently, recruitment is mainly done through Facebook groups:\n" +
        "Traditional recruitment websites are not suitable for students\n" +
        "J4U Startup Ideas\n" +
        "Web-based platform\n" +
        "Two-sided marketplace\n" +
        "Trust & reputation system\n" +
        "Target Customers & Business Focus\n" +
        "Customer Segments:\n" +
        "Students: Seeking part-time jobs, internships, and freelance opportunities\n" +
        "Group Name: J4U_EXE101\n" +
        "Business Area\n" +
        "HRTech\n" +
        "Student Freelance Marketplace\n" +
        "Member Major Roles\n" +
        "Bùi Mạnh Thịnh Software Engineering CEO / Team Leader\n" +
        "Lê Trung Kiên Software Engineering CTO\n" +
        "Members and Roles";

    private static readonly ChecklistItem[] Checklist =
    [
        new(0, "Requirement", "Team Name", null),
        new(1, "Requirement", "Startup Idea", null),
        new(2, "Requirement", "Startup Field", null),
        new(3, "Requirement", "Member Roles", null)
    ];

    [Theory]
    [InlineData(0, "Group Name")]
    [InlineData(1, "Project Name")]
    [InlineData(3, "CEO")]
    public void Find_RecognisesWhatTheSlidesCallTheSameThing(int index, string quoted)
    {
        var findings = CheckpointSummaryEvidence.Find(Deck, Checklist, CheckpointSummaryTopics.None);

        findings[index].Finding.Should().Be(CheckpointSummaryEvidence.Related, findings[index].Title);
        findings[index].Evidence.Should().Contain(quoted, findings[index].Title);
    }

    [Fact]
    public void Find_CountsABusinessAreaAsTheStartupFieldEvenWhenItsLinesAreTooShortToQuote()
    {
        var findings = CheckpointSummaryEvidence.Find(Deck, Checklist, CheckpointSummaryTopics.None);

        findings[2].Finding.Should().Be(CheckpointSummaryEvidence.Related);
        findings[2].Cues.Should().Contain(cue => cue.Label == "Startup field" && cue.Found);
    }

    [Fact]
    public void Find_QuotesALabelWithItsValueAloneAndTheQuoteCanBePlacedOnItsPage()
    {
        var findings = CheckpointSummaryEvidence.Find(Deck, Checklist, CheckpointSummaryTopics.None);
        // The next line of the text belongs to another slide: gluing it on would make a quote that is on no page.
        findings[0].Evidence.Should().Be("Group Name: J4U");

        var pages = new[] { "J4U Introduction to the Project", "Introduction Project Name: J4U – Jobs for You Group Name: J4U", "Problem Statement There is a huge demand" };
        var locator = CheckpointPageLocator.From(new ExtractedText(string.Join(' ', pages), pages.Length, pages))!;

        locator.Locate(findings[0].Evidence).Should().Be(2);
    }

    [Fact]
    public void Find_StillReportsMissingWhatTheDeckNeverSays()
    {
        const string deck = "Introduction\nWe built a small app.\nThank you";
        var findings = CheckpointSummaryEvidence.Find(deck, Checklist, CheckpointSummaryTopics.None);

        findings.Take(3).Should().OnlyContain(finding => finding.Finding == CheckpointSummaryEvidence.NotFound);
    }

    [Fact]
    public void Prepare_DoesNotCallAnIntroductionDeckWithEveryContentSlideAsTextMostlyImages()
    {
        // 9 pages, about 1,500 characters: logos and photos are decoration, every slide that has content has its text.
        var text = string.Join('\n', Enumerable.Range(0, 27).Select(position => $"Line {position}: " + new string('x', 52)));
        var extracted = new ExtractedText(text, 9);

        var prepared = CheckpointFileText.Prepare(extracted, ".pdf", 20_000);

        prepared.Value.LittleText.Should().BeFalse();
    }

    private const string Market =
        "Vietnam has about 2 million university students, and around 70% of them look for part-time or freelance work to cover living costs.\n" +
        "Pilot survey (n = 120 students in Ho Chi Minh City): 72% want flexible short-term jobs, 58% found their last job through Facebook groups, 41% met a fake or unclear job post, and 35% were paid late or not in full.\n" +
        "Interviews with 5 small business owners: 4 of 5 struggle to find a reliable part-time worker within 48 hours.\n" +
        "Method: an online questionnaire with 5-level Likert questions, plus 15-minute interviews; results were analysed with descriptive statistics.\n" +
        "Key insight: both sides need trust and speed, not just a longer list of jobs.";

    [Fact]
    public void Find_QuotesTheProblemRatherThanTheMethodForAProblemCriterion()
    {
        ChecklistItem[] checklist = [new(0, "Criterion", "Problem Identification", "Problem is clearly identified with supporting research",
            20, "Document", "Problem is clearly identified with supporting research and evidence")];

        var findings = CheckpointSummaryEvidence.Find(Market, checklist, CheckpointSummaryTopics.None);

        findings[0].Evidence.Should().NotStartWith("Method");
        findings[0].Evidence.Should().ContainAny("fake", "struggle");
    }

    [Fact]
    public void StatusLines_DoNotReadProseContainingTheWordDoneAsAStatus()
    {
        CheckpointStatusLines.Find("Currently, recruitment is mainly done through Facebook groups:").Should().BeEmpty();
        CheckpointStatusLines.Find("Market Research Completed Problem validated").Should().ContainSingle();
    }

    [Fact]
    public void TryBuild_WhenTheModelOverviewIsRejected_UsesTheStatedIdeaOfTheFile()
    {
        var preview = CheckpointSummaryEvidence.Preview(Deck, Checklist, CheckpointSummaryTopics.None);
        var grounding = new CheckpointSummaryGrounding(Deck);

        var content = CheckpointSummaryOutput.TryBuild("{\"overview\":\"Zxqv wlmn kprt qqqq jjjj.\",\"keyPoints\":[],\"questions\":[]}", preview, grounding);

        content!.OverviewFromSource.Should().BeTrue();
        content.Overview.Should().Contain("platform connecting students").And.NotContain("Quote");
    }

    private const string IdeaDeck =
        "Introduction\n" +
        "Project Name: J4U – Jobs for You\n" +
        "A platform connecting students with short-term jobs and a dedicated community\n" +
        "Group Name: J4U\n" +
        "Team Name: J4U\n" +
        "Startup Field: HRTech – student freelance marketplace\n" +
        "Startup Idea: a trusted two-sided platform where students find safe short-term jobs and small businesses find reliable part-time help.\n" +
        "Core feature 4: two-way ratings and reviews after every task, which build a reputation system.";

    [Fact]
    public void Find_PrefersTheSentenceThatDescribesTheIdeaOverTheProjectName()
    {
        ChecklistItem[] checklist = [new(0, "Criterion", "Startup Idea Clarity", "Idea is clear and compelling", 30,
            CheckpointCriterionProfile.Overall, "Idea is clear, original and well explained")];

        var findings = CheckpointSummaryEvidence.Find(IdeaDeck, checklist, CheckpointSummaryTopics.None);

        findings[0].Evidence.Should().Contain("two-sided platform");
    }

    [Fact]
    public void AssumesUnseenFact_RejectsAQuestionThatDeniesWhatTheFileStates()
    {
        var grounding = new CheckpointSummaryGrounding(IdeaDeck);

        grounding.AssumesUnseenFact("Làm thế nào để đảm bảo an toàn trong môi trường chưa có hệ thống đánh giá?").Should().BeTrue();
        // Asking about something the file really lacks stays allowed.
        grounding.AssumesUnseenFact("Nhóm chưa có kế hoạch gọi vốn, vậy nhóm định thu phí như thế nào?").Should().BeFalse();
    }

    [Fact]
    public void DiagnoseOverview_NamesTheReasonWithoutQuotingAnything()
    {
        var grounding = new CheckpointSummaryGrounding(IdeaDeck);

        var reasons = grounding.DiagnoseOverview("Nền tảng đạt 9.999 người dùng. Zxqv wlmn kprt qqqq jjjj.");

        reasons.Should().HaveCount(2);
        reasons[0].Should().Be("invented-number");
        reasons[1].Should().StartWith("low-word-support");
    }

    [Fact]
    public void FindOverviewEvidence_CitesEachPassageThatBacksPartOfTheSentence()
    {
        var grounding = new CheckpointSummaryGrounding(IdeaDeck);

        // The name is on one line and the platform on the next: no single line holds both, two neighbouring lines do.
        var passages = grounding.FindOverviewEvidence("J4U là platform kết nối sinh viên với việc làm ngắn hạn.");

        passages.Should().NotBeEmpty();
        passages.Should().Contain(passage => passage.Contains("platform"));
        grounding.Diagnose("J4U là platform kết nối sinh viên với việc làm ngắn hạn.").Should().Be("supported");
        grounding.Diagnose("J4U là platform giúp 500 sinh viên kiếm việc.").Should().Be("invented-number");
    }

    [Fact]
    public void FindOverviewEvidence_RejectsASentenceWhoseWordsAreScatteredAcrossTheFile()
    {
        var filler = string.Join("\n", Enumerable.Range(0, 8).Select(position => $"Unrelated filler line number{position} about nothing."));
        var deck = "Marketplace overview" + "\n" + filler + "\n" + "Backend roadmap" + "\n" + filler + "\n" + "Revenue commission" + "\n" + filler + "\n" + "Facebook groups";
        var grounding = new CheckpointSummaryGrounding(deck);

        // One word from each far-apart line: nothing adds two new terms, so no passage is cited and the sentence is not accepted.
        grounding.FindOverviewEvidence("Đây là marketplace với backend, commission và facebook.").Should().BeEmpty();
    }

    [Fact]
    public void Find_GivesACriterionAndARequirementAboutTheIdeaTheSentenceThatDescribesIt()
    {
        ChecklistItem[] checklist =
        [
            new(0, "Criterion", "Startup Idea Clarity", "Idea is innovative, clearly articulated, and easy to understand", 30,
                CheckpointCriterionProfile.Overall, "Idea is innovative, clearly articulated, and easy to understand"),
            new(1, "Requirement", "Startup Idea", null)
        ];

        var findings = CheckpointSummaryEvidence.Find(IdeaDeck, checklist, CheckpointSummaryTopics.None);

        findings[0].Evidence.Should().Contain("two-sided platform");
        findings[1].Evidence.Should().Contain("two-sided platform");
    }

    private const string EnglishOverview = "J4U is a trusted two-sided platform where students find safe short-term jobs and small businesses find reliable part-time help.";
    private const string Vietnamese = "J4U là nền tảng hai phía đáng tin cậy, nơi sinh viên tìm việc ngắn hạn an toàn và doanh nghiệp nhỏ tìm người làm bán thời gian.";
    private const string Invented = "Zxqv wlmn kprt qqqq jjjj.";

    private static string AnswerWith(string overview, params string[] translations) =>
        System.Text.Json.JsonSerializer.Serialize(new { overview, overviewVi = translations, keyPoints = Array.Empty<string>(), questions = Array.Empty<string>() });

    private static WorkspaceFileSummaryContent Build(string answer) => CheckpointSummaryOutput.TryBuild(answer,
        CheckpointSummaryEvidence.Preview(IdeaDeck, Checklist, CheckpointSummaryTopics.None), new CheckpointSummaryGrounding(IdeaDeck))!;

    [Fact]
    public void TryBuild_ShowsTheVietnameseTranslationOfAnOverviewThatWasCheckedInEnglish()
    {
        var content = Build(AnswerWith(EnglishOverview, Vietnamese));

        content.OverviewFromSource.Should().BeFalse();
        content.Overview.Should().StartWith("J4U là nền tảng");
        content.OverviewEvidence.Should().NotBeEmpty("the English sentence is still cited from the document");
    }

    [Fact]
    public void TryBuild_KeepsTheEnglishOverviewWhenTheTranslationChangesAFigure()
    {
        var content = Build(AnswerWith(EnglishOverview, "J4U là nền tảng hai phía đáng tin cậy với 500 sinh viên tìm việc ngắn hạn an toàn."));

        content.Overview.Should().StartWith("J4U is a trusted");
    }

    [Fact]
    public void TryBuild_DropsABulletMarkThatTheModelPutInFrontOfTheTranslation()
    {
        var content = Build(AnswerWith(EnglishOverview, "* " + Vietnamese));

        content.Overview.Should().StartWith("J4U là nền tảng");
    }

    [Fact]
    public void TryBuild_DropsAnUnbackedSentenceTogetherWithItsTranslationAndKeepsTheRest()
    {
        var content = Build(AnswerWith(EnglishOverview + " " + Invented, Vietnamese, "Một câu bịa hoàn toàn không có trong tài liệu."));

        content.Overview.Should().StartWith("J4U là nền tảng").And.NotContain("bịa");
    }

    [Fact]
    public void TryBuild_KeepsEnglishWhenTheTranslationHasAnotherNumberOfSentences()
    {
        var content = Build(AnswerWith(EnglishOverview, Vietnamese, "Thêm một câu nữa."));

        content.Overview.Should().StartWith("J4U is a trusted");
    }

    [Fact]
    public void FindEvidence_DoesNotLetAFewCommonEnglishWordsBackAnEnglishClaim()
    {
        var grounding = new CheckpointSummaryGrounding(IdeaDeck);

        // A few common words of the file ("team", "two", "students") do not back a claim the file never makes.
        grounding.FindEvidence("The team plans a Series A funding round and an IPO for two million students.").Should().BeNull();
        // A paraphrase that keeps several of the file's own words is backed.
        grounding.FindEvidence("The startup offers a trusted platform where students find safe short-term jobs.").Should().NotBeNull();
        grounding.Supports("J4U is a trusted two-sided platform where students find safe short-term jobs.").Should().BeTrue();
    }
}
