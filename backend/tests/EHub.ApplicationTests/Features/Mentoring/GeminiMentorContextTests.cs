using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.Scoring;
using EHub.Domain.Entities;
using EHub.Infrastructure.Mentoring.Gemini;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class GeminiMentorContextTests
{
    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task AProjectCanCompareFiveSourceBackedCandidatesButCannotExceedTheBudget(int count, bool accepted)
    {
        using var rig = new Rig(input => Output(input, count));
        var profiles = Enumerable.Range(0, 6).Select(_ => Profile()).ToArray();
        var task = rig.Ranker.RankAsync([new(Guid.NewGuid(), "Marketing support")], profiles, MentorRankingScope.AllSpecialties, default);
        if (!accepted)
        {
            (await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => task)).Failure.Should().Be(MentorFitFailure.InvalidResponse);
            return;
        }
        var row = (await task).Values.Single();
        row.Values.Count(value => value.HasEvidence).Should().Be(5);
        row.Values.Count(value => !value.HasEvidence).Should().Be(1);
        rig.MaximumCandidates.Should().Be(5);
    }

    [Fact]
    public async Task FitDetailsExposeExactProfessionalSourcesAndProjectNeedsWithoutInventingConfidence()
    {
        using var rig = new Rig(input => Output(input, 1, ["Marketing support"]));
        var profile = Profile();
        profile.Expertise = ["Marketing"];
        var raw = (await rig.Ranker.RankAsync([new(Guid.NewGuid(), "Marketing support")], [profile], MentorRankingScope.AllSpecialties, default))
            .Values.Single().Values.Single();
        var combined = MentorCombinedScoring.Combine(raw, new MentorScoringOptions());
        combined.Analysis!.ProfileScore.Should().Be(26.25);
        combined.Analysis.HasEvidence.Should().BeTrue();
        combined.Analysis.AvailableProfileFields.Should().BeEquivalentTo("experience", "expertise");
        combined.Analysis.ProjectNeeds.Should().Equal("Marketing support");
        combined.Analysis.Criteria.Single(criterion => criterion.Key == "experience").Evidence.Should().Be("Marketing strategy");
        combined.Analysis.Criteria.Single(criterion => criterion.Key == "technology").Score.Should().Be(0);
        MentorCombinedScoring.Combine(raw, new MentorScoringOptions(), hasContradiction: true).Analysis!.HasEvidence.Should().BeFalse();
        MentorCombinedScoring.Combine(raw, new MentorScoringOptions(), hasContradiction: true).Analysis!.Criteria
            .Should().OnlyContain(item => item.Score == 0 && item.Evidence == null);
    }

    [Theory]
    [InlineData("Unstated fundraising requirement")]
    [InlineData("[removed]")]
    public async Task ModelCannotInventProjectNeedsOrQuoteMaskedData(string needs)
    {
        using var rig = new Rig(input => Output(input, 1, [needs]));
        var task = rig.Ranker.RankAsync([new(Guid.NewGuid(), "Marketing support")], [Profile()], MentorRankingScope.AllSpecialties, default);
        (await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => task)).Failure.Should().Be(MentorFitFailure.InvalidResponse);
    }

    [Fact]
    public void RelevantLatePassagesAreSelectedFromLongProfilesInsteadOfAlwaysTakingThePrefix()
    {
        var profile = Profile();
        profile.Experience = string.Join("; ", Enumerable.Range(0, 100).Select(index => $"Finance workshop number {index}")) +
            "; Delivered React prototypes for product validation";
        var prompt = new GeminiMentorPrompt([new(Guid.NewGuid(), "Need a React prototype")], [profile], MentorRankingScope.AllSpecialties);
        prompt.Mentors.Single().Fields["experience"].Should().Contain("Delivered React prototypes for product validation");
        prompt.Mentors.Single().Fields["experience"].Length.Should().BeLessThanOrEqualTo(1200);
        prompt.Mentors.Single().EvidenceOptions["experience"].Values.Should().Contain("Delivered React prototypes for product validation");
        prompt.Json.Should().NotContain("SourceFields").And.NotContain("sourceFields");
    }

    [Fact]
    public async Task AWordInsideADeniedProjectRequirementCannotBePresentedAsARequestedNeed()
    {
        using var rig = new Rig(input => Output(input, 1, ["AI"]));
        var task = rig.Ranker.RankAsync([new(Guid.NewGuid(), "Marketing support; AI is not required")],
            [Profile()], MentorRankingScope.AllSpecialties, default);
        (await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => task)).Failure.Should().Be(MentorFitFailure.InvalidResponse);
    }

    [Fact]
    public void FullSourceContextStillControlsEvidenceWhenARetrievedPassageContainsADenial()
    {
        var profile = Profile();
        profile.Experience = "No React experience; " + string.Join("; ", Enumerable.Repeat("Finance workshops", 100));
        var prompt = new GeminiMentorPrompt([new(Guid.NewGuid(), "React prototype")], [profile], MentorRankingScope.AllSpecialties);
        prompt.Mentors.Single().SourceFields["experience"].Should().Contain("No React experience");
        prompt.Mentors.Single().EvidenceOptions["experience"].Values.Should().NotContain(quote => quote.Contains("React"));
    }

    [Fact]
    public async Task PresentButUnrelatedFieldsAreNotReportedAsMissing()
    {
        using var rig = new Rig(input => Output(input, 1));
        var profile = Profile();
        profile.TechnologySkills = ["No React experience"];
        var fit = (await rig.Ranker.RankAsync([new(Guid.NewGuid(), "Marketing support")], [profile], MentorRankingScope.AllSpecialties, default))
            .Values.Single().Values.Single();
        fit.Analysis!.AvailableProfileFields.Should().Contain("technology");
        fit.Analysis.Criteria.Single(item => item.Key == "technology").Score.Should().Be(0);
    }

    [Fact]
    public async Task OversizedOutputSplitsOutsideTheRequestGateAndReturnsAllOriginalProjectIds()
    {
        using var rig = new Rig(input => Output(input, 1), truncateAbove: 2);
        var projects = Enumerable.Range(0, 5).Select(index => new MentorRankingProject(Guid.NewGuid(), $"Marketing support {index}")).ToArray();
        var result = await rig.Ranker.RankAsync(projects, [Profile()], MentorRankingScope.AllSpecialties, default)
            .WaitAsync(TimeSpan.FromSeconds(5));
        result.Keys.Should().BeEquivalentTo(projects.Select(project => project.Id));
        result.Values.Should().OnlyContain(row => row.Values.Single().HasEvidence);
        rig.ProjectCounts.Should().Equal(5, 2, 3, 1, 2);
    }

    [Fact]
    public async Task AnOversizedSingleProjectStopsWithASafeErrorInsteadOfRecursingForever()
    {
        using var rig = new Rig(input => Output(input, 1), truncateAbove: 0);
        var task = rig.Ranker.RankAsync([new(Guid.NewGuid(), "Marketing support")], [Profile()], MentorRankingScope.AllSpecialties, default);
        (await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => task)).Failure.Should().Be(MentorFitFailure.OutputLimit);
        rig.ProjectCounts.Should().Equal(1);
    }

    private static MentorProfile Profile() => new()
    {
        Experience = "Marketing strategy",
        User = new User { FullName = "Synthetic mentor", Email = "fictional@example.test" }
    };

    private static object Output(JsonElement input, int count, string[]? needs = null) => new
    {
        results = input.GetProperty("projects").EnumerateArray().Select(project => new
        {
            project = project.GetProperty("id").GetString(), needs = needs ?? [],
            mentors = input.GetProperty("mentors").EnumerateArray().Take(count).Select(mentor => new
            {
                id = mentor.GetProperty("id").GetString(), criteria = new
                {
                    experience = new { score = 75, evidence = "E01" }, expertise = new { score = 0, evidence = "" },
                    startupDomain = new { score = 0, evidence = "" }, technology = new { score = 0, evidence = "" },
                    bio = new { score = 0, evidence = "" }
                }
            }).ToArray()
        }).ToArray()
    };

    private sealed class Rig : IDisposable
    {
        private readonly HttpClient client;
        private readonly GeminiRequestCoordinator coordinator = new();
        public GeminiMentorFitRanker Ranker { get; }
        public int MaximumCandidates { get; private set; }
        public List<int> ProjectCounts { get; } = [];
        public Rig(Func<JsonElement, object> respond, int? truncateAbove = null)
        {
            client = new HttpClient(new Handler(async (request, ct) =>
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                MaximumCandidates = body.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema")
                    .GetProperty("properties").GetProperty("results").GetProperty("items").GetProperty("properties")
                    .GetProperty("mentors").GetProperty("maxItems").GetInt32();
                using var input = JsonDocument.Parse(body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
                var count = input.RootElement.GetProperty("projects").GetArrayLength();
                ProjectCounts.Add(count);
                var finish = truncateAbove.HasValue && count > truncateAbove.Value ? "MAX_TOKENS" : "STOP";
                var text = JsonSerializer.Serialize(respond(input.RootElement));
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
                    { candidates = new[] { new { finishReason = finish, content = new { parts = new[] { new { text } } } } } }) };
            }));
            Ranker = new(client, Options.Create(new GeminiMentorOptions { ApiKey = "SYNTHETIC_CONTEXT_TEST_KEY",
                DataTransferEnabled = true, MinimumRequestIntervalSeconds = 0, FallbackModels = "" }),
                coordinator, TimeProvider.System, NullLogger<GeminiMentorFitRanker>.Instance);
        }
        public void Dispose() { client.Dispose(); coordinator.Gate.Dispose(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
}
