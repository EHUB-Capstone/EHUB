using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.Infrastructure;
using EHub.Infrastructure.Mentoring;
using EHub.Application.Features.Mentoring.Scoring;
using EHub.Infrastructure.Mentoring.Gemini;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class GeminiMentorFitRankerTests
{
    private const string Evidence = "Digital marketing and startup growth";
    private const string FakeKey = "OFFLINE_TEST_NOT_A_REAL_API_KEY=";

    [Theory]
    [InlineData("E01", true)]
    [InlineData("E99", false)]
    public async Task EvidenceIdsResolveOnlyToSuppliedProfessionalFragments(string evidenceId, bool valid)
    {
        using var rig = new Rig(call =>
        {
            var result = Result(call);
            result["results"]![0]!["mentors"]![0]!["criteria"]!["experience"]!["evidence"] = evidenceId;
            return Envelope(result.ToJsonString());
        });
        if (!valid)
        {
            await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => Rank(rig));
            return;
        }
        var rankings = await Rank(rig);
        rankings.Values.Single().Values.Single().Reasons.Should().Contain(reason => reason.Contains(Evidence));
    }

    [Theory]
    [InlineData(100, 5)]
    [InlineData(300, 15)]
    public async Task BoundedBatchesHandleSupportedScopesAndReturnEveryCandidateWithoutSavingAssignments(int projectCount, int expectedCalls)
    {
        using var rig = new Rig();
        var mentors = new[] { Profile(), Profile() };
        var projects = Enumerable.Range(1, projectCount).Select(index => new MentorRankingProject(Guid.NewGuid(),
            $"Fictional marketing startup {index}, seeking customer acquisition coaching.")).ToArray();

        var results = await rig.Ranker.RankAsync(projects, mentors, MentorRankingScope.AllSpecialties, default);

        rig.Calls.Should().HaveCount(expectedCalls);
        var sentProjects = rig.Calls.SelectMany(call =>
        {
            using var request = JsonDocument.Parse(call.Body);
            using var input = JsonDocument.Parse(request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
            input.RootElement.GetProperty("projects").GetArrayLength().Should().BeLessThanOrEqualTo(20);
            return input.RootElement.GetProperty("projects").EnumerateArray().Select(project => project.GetProperty("text").GetString()).ToArray();
        }).ToArray();
        sentProjects.Should().HaveCount(projectCount).And.OnlyHaveUniqueItems();
        results.Should().HaveCount(projectCount);
        results.Values.Should().OnlyContain(row => row.Count == 2 && row.Values.Count(fit => fit.HasEvidence) == 1);
        results.Values.SelectMany(row => row.Values).Where(fit => fit.HasEvidence).Should().OnlyContain(fit =>
            fit.Score == 0 && fit.Reasons.Any(reason => reason.Contains("gemini-3.8-flash")) && fit.Criteria!.Experience == 75);
        rig.Calls[0].Body.Should().NotContain(mentors[0].Id.ToString()).And.NotContain(projects[0].Id.ToString());
    }

    [Fact]
    public async Task ATruncatedLaterBatchRejectsTheWholeScopeAndRetryReusesTheCompletedBatch()
    {
        var calls = 0;
        using var rig = new Rig(call => ++calls == 2 ? Envelope("truncated", "MAX_TOKENS") : Success(call));
        var projects = Enumerable.Range(0, 41).Select(index => new MentorRankingProject(Guid.NewGuid(),
            $"Fictional marketing startup {index}")).ToArray();
        var mentors = new[] { Profile() };

        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() =>
            rig.Ranker.RankAsync(projects, mentors, MentorRankingScope.AllSpecialties, default));
        rig.Calls.Should().HaveCount(2);
        var result = await rig.Ranker.RankAsync(projects, mentors, MentorRankingScope.AllSpecialties, default);
        result.Should().HaveCount(41);
        rig.Calls.Should().HaveCount(4, "the first successful batch is cached and the failed batch is retried");
    }

    [Theory]
    [InlineData("gemini-3.8-flash", "low")]
    [InlineData("gemini-3.5-flash-lite", "minimal")]
    [InlineData("gemini-3.1-flash-lite", "low")]
    [InlineData("gemini-2.5-flash-lite", "off")]
    public async Task SendsOnlyApprovedModelsWithTheirSupportedThinkingConfiguration(string model, string thinking)
    {
        using var rig = new Rig();
        rig.Settings.Model = model;
        await Rank(rig);
        var call = rig.Calls.Single();
        call.Model.Should().Be(model);
        call.Key.Should().Be(FakeKey);
        call.Uri.Scheme.Should().Be("https");
        call.Uri.Host.Should().Be("generativelanguage.googleapis.com");
        call.Uri.Query.Should().BeEmpty();
        using var json = JsonDocument.Parse(call.Body);
        var config = json.RootElement.GetProperty("generationConfig");
        config.GetProperty("responseMimeType").GetString().Should().Be("application/json");
        if (thinking == "off") config.GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32().Should().Be(0);
        else config.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString().Should().Be(thinking);
    }

    [Fact]
    public async Task ACombinedRolePoolIsRejectedBeforeItCanSilentlyOmitBusinessCandidates()
    {
        using var rig = new Rig();
        var business = Profile();
        var technical = Profile();
        technical.Type = EHub.Domain.Enums.MentorType.Academic;
        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => rig.Ranker.RankAsync(
            [new(Guid.NewGuid(), "Marketing and React")], [business, technical], MentorRankingScope.AllSpecialties, default));
        rig.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task VietnameseInputReachesTheModelAsNaturalTextInsteadOfUnicodeEscapeTokens()
    {
        using var rig = new Rig();
        var mentor = Profile();
        mentor.Experience += "; Có kinh nghiệm phát triển sản phẩm và tiếp thị.";
        await rig.Ranker.RankAsync([new(Guid.NewGuid(), "Dự án cần cố vấn tiếp thị và phát triển sản phẩm")],
            [mentor], MentorRankingScope.AllSpecialties, default);
        using var request = JsonDocument.Parse(rig.Calls.Single().Body);
        var modelInput = request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
        modelInput.Should().Contain("phát triển sản phẩm").And.NotContain("\\u");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModelSpecificQuotaUsesAnApprovedFallbackAndRemembersTheCooldown(bool daily)
    {
        using var rig = new Rig(call => call.Model == "gemini-3.8-flash" ? Quota(call.Model, daily) : Success(call));
        var result = await Rank(rig);
        result.Values.Single().Values.Single().Reasons.Should().Contain(reason => reason.Contains("gemini-3.5-flash-lite"));
        rig.Calls.Select(call => call.Model).Should().Equal("gemini-3.8-flash", "gemini-3.5-flash-lite");
        var blocked = rig.Coordinator.ModelBlockedUntil["gemini-3.8-flash"];
        (blocked - rig.Clock.GetUtcNow()).Should().BeGreaterThan(daily ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(30));
        await Rank(rig, "A different fictional marketing project.");
        rig.Calls.Last().Model.Should().Be("gemini-3.5-flash-lite");
        rig.Calls.Count(call => call.Model == "gemini-3.8-flash").Should().Be(1);
    }

    [Fact]
    public async Task MissingModelUsesTheNextApprovedModel()
    {
        using var rig = new Rig(call => call.Model == "gemini-3.8-flash" ? Error(HttpStatusCode.NotFound) : Success(call));
        await Rank(rig);
        rig.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task ALongRetryAfterIsNotShortenedToAnEarlierRetry()
    {
        using var rig = new Rig(call =>
        {
            if (call.Model != "gemini-3.8-flash") return Success(call);
            var response = Quota(call.Model, false);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(2));
            return response;
        });
        await Rank(rig);
        rig.Coordinator.ModelBlockedUntil["gemini-3.8-flash"].Should().Be(rig.Clock.GetUtcNow().AddHours(2));
    }

    [Fact]
    public async Task EmptyShortlistsRemainZeroScoreForAdminReview()
    {
        using var rig = new Rig(call =>
        {
            var result = Result(call);
            foreach (var row in (JsonArray)result["results"]!) row!["mentors"] = new JsonArray();
            return Envelope(result.ToJsonString());
        });
        var results = await Rank(rig);
        var fit = results.Values.Single().Values.Single();
        fit.Score.Should().Be(0);
        fit.HasEvidence.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizedProjectOrMentorCollectionsAreRejectedBeforeSending(bool tooManyProjects)
    {
        using var rig = new Rig();
        var projects = Enumerable.Range(0, tooManyProjects ? 301 : 1)
            .Select(_ => new MentorRankingProject(Guid.NewGuid(), "Marketing startup")).ToArray();
        var mentors = Enumerable.Range(0, tooManyProjects ? 1 : 201).Select(_ => Profile()).ToArray();
        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => rig.Ranker.RankAsync(
            projects, mentors, MentorRankingScope.AllSpecialties, default));
        rig.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedOrMixedQuotaStopsWithoutTryingOtherModels(bool mixed)
    {
        using var rig = new Rig(call => Quota(call.Model, false, shared: true, mixed: mixed));
        var action = () => Rank(rig);
        await action.Should().ThrowAsync<MentorFitRankerUnavailableException>();
        await action.Should().ThrowAsync<MentorFitRankerUnavailableException>();
        rig.Calls.Should().HaveCount(1, "a shared cooldown also prevents another request from hammering the project");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task OtherErrorsStopAndDoNotExposeTheProviderBody(HttpStatusCode status)
    {
        using var rig = new Rig(_ => Error(status));
        var exception = await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => Rank(rig));
        exception.ToString().Should().NotContain(FakeKey).And.NotContain("PRIVATE_PROVIDER_BODY");
        rig.Calls.Should().HaveCount(status == HttpStatusCode.ServiceUnavailable ? 6 : 1);
    }

    [Fact]
    public async Task ATransient503RetriesTheSameModelOnceBeforeReturningTheCompleteResult()
    {
        var calls = 0;
        using var rig = new Rig(call => ++calls == 1 ? Error(HttpStatusCode.ServiceUnavailable) : Success(call));
        var result = await Rank(rig);
        result.Should().ContainSingle();
        rig.Calls.Select(call => call.Model).Should().Equal("gemini-3.8-flash", "gemini-3.8-flash");
    }

    [Fact]
    public async Task Repeated503UsesAnApprovedFallbackAndCoolsDownTheUnavailableModel()
    {
        using var rig = new Rig(call => call.Model == "gemini-3.8-flash" ? Error(HttpStatusCode.ServiceUnavailable) : Success(call));
        await Rank(rig);
        rig.Calls.Select(call => call.Model).Should().Equal("gemini-3.8-flash", "gemini-3.8-flash", "gemini-3.5-flash-lite");
        rig.Coordinator.ModelBlockedUntil["gemini-3.8-flash"].Should().Be(rig.Clock.GetUtcNow().AddMinutes(1));
        await Rank(rig, "Another fictional marketing project");
        rig.Calls.Last().Model.Should().Be("gemini-3.5-flash-lite");
    }

    [Fact]
    public async Task ExhaustingAllModelQuotasStopsAfterThreeCalls()
    {
        using var rig = new Rig(call => Quota(call.Model, true));
        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => Rank(rig));
        rig.Calls.Select(call => call.Model).Should().Equal(rig.Settings.Models);
    }

    [Fact]
    public async Task UnspecifiedQuotaDoesNotBlindlySwitchModels()
    {
        using var rig = new Rig(_ => Error(HttpStatusCode.TooManyRequests));
        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => Rank(rig));
        rig.Calls.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("unknown-project")]
    [InlineData("missing-project")]
    [InlineData("duplicate-project")]
    [InlineData("unknown-mentor")]
    [InlineData("duplicate-mentor")]
    [InlineData("too-many-per-role")]
    [InlineData("missing-criteria")]
    [InlineData("missing-criterion")]
    [InlineData("bad-criterion")]
    [InlineData("negative-criterion")]
    [InlineData("fractional-criterion")]
    [InlineData("invented-criterion-evidence")]
    [InlineData("wrong-field-evidence")]
    [InlineData("zero-with-evidence")]
    [InlineData("all-zero-criteria")]
    [InlineData("truncated")]
    [InlineData("invalid-json")]
    public async Task InvalidOrIncompleteOutputIsRejectedAsAWhole(string fault)
    {
        using var rig = new Rig(call =>
        {
            var data = Result(call);
            var rows = (JsonArray)data["results"]!;
            var row = (JsonObject)rows[0]!;
            var candidates = (JsonArray)row["mentors"]!;
            var candidate = (JsonObject)candidates[0]!;
            switch (fault)
            {
                case "unknown-project": row["project"] = "P999"; break;
                case "missing-project": rows.Clear(); break;
                case "duplicate-project": rows.Add(row.DeepClone()); break;
                case "unknown-mentor": candidate["id"] = "M999"; break;
                case "duplicate-mentor": candidates.Add(candidate.DeepClone()); break;
                case "missing-criteria": candidate.Remove("criteria"); break;
                case "missing-criterion": ((JsonObject)candidate["criteria"]!).Remove("technology"); break;
                case "bad-criterion": candidate["criteria"]!["experience"]!["score"] = 101; break;
                case "negative-criterion": candidate["criteria"]!["experience"]!["score"] = -1; break;
                case "fractional-criterion": candidate["criteria"]!["experience"]!["score"] = 75.5; break;
                case "invented-criterion-evidence": candidate["criteria"]!["experience"]!["evidence"] = "Invented professional background"; break;
                case "wrong-field-evidence":
                    candidate["criteria"]!["bio"] = new JsonObject { ["score"] = 100, ["evidence"] = Evidence }; break;
                case "zero-with-evidence": candidate["criteria"]!["experience"]!["score"] = 0; break;
                case "all-zero-criteria":
                    candidate["criteria"]!["experience"] = new JsonObject { ["score"] = 0, ["evidence"] = "" }; break;
                case "too-many-per-role":
                    candidates.Add(new JsonObject { ["id"] = "M002", ["criteria"] = Criteria() });
                    candidates.Add(new JsonObject { ["id"] = "M003", ["criteria"] = Criteria() });
                    break;
            }
            return Envelope(fault == "invalid-json" ? "not-json" : data.ToJsonString(), fault == "truncated" ? "MAX_TOKENS" : "STOP");
        });
        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => rig.Ranker.RankAsync(
            [new(Guid.NewGuid(), "Marketing startup")], [Profile(), Profile(), Profile()], MentorRankingScope.AllSpecialties, default));
        rig.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnExactQuoteInsideADenialIsNotPositiveEvidence()
    {
        using var rig = new Rig();
        var mentor = Profile();
        mentor.Experience = "I have no " + Evidence;
        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => rig.Ranker.RankAsync(
            [new(Guid.NewGuid(), "Marketing startup")], [mentor], MentorRankingScope.AllSpecialties, default));
    }

    [Fact]
    public async Task StructuredFieldsSupportSemanticRatingsAndBackendIgnoresModelSuppliedWeightsAndTotals()
    {
        using var rig = new Rig(call =>
        {
            var data = Result(call);
            var candidate = data["results"]![0]!["mentors"]![0]!;
            candidate["criteria"] = new JsonObject
            {
                ["experience"] = new JsonObject { ["score"] = 90, ["evidence"] = Evidence },
                ["expertise"] = new JsonObject { ["score"] = 80, ["evidence"] = "Growth" },
                ["startupDomain"] = new JsonObject { ["score"] = 60, ["evidence"] = "Fintech" },
                ["technology"] = new JsonObject { ["score"] = 70, ["evidence"] = "AI" },
                ["bio"] = new JsonObject { ["score"] = 40, ["evidence"] = "Business graduate" }
            };
            candidate["finalScore"] = 100;
            candidate["experienceWeight"] = 100;
            candidate["score"] = 999;
            candidate["evidence"] = "Ignored legacy overall evidence";
            return Envelope(data.ToJsonString());
        });
        var mentor = Profile();
        mentor.Expertise = ["Growth"];
        mentor.StartupDomains = ["Fintech"];
        mentor.TechnologySkills = ["AI"];
        mentor.Bio = "Business graduate";
        var results = await rig.Ranker.RankAsync([new(Guid.NewGuid(), "Startup finance AI growth")], [mentor], MentorRankingScope.AllSpecialties, default);
        var fit = results.Values.Single()[mentor.Id];
        fit.Criteria.Should().Be(new MentorProfileFitCriteria(90, 80, 60, 70, 40));
        MentorCombinedScoring.Combine(fit, new()).Score.Should().Be(54); // 76.5 * .7 = 53.55; legacy overall fields are ignored.
        using var request = JsonDocument.Parse(rig.Calls.Single().Body);
        var responseProperties = request.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema")
            .GetProperty("properties").GetProperty("results").GetProperty("items").GetProperty("properties")
            .GetProperty("mentors").GetProperty("items").GetProperty("properties");
        responseProperties.TryGetProperty("score", out _).Should().BeFalse();
        responseProperties.TryGetProperty("evidence", out _).Should().BeFalse();
        using var input = JsonDocument.Parse(request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
        input.RootElement.GetProperty("mentors")[0].GetProperty("profile").GetProperty("technology").GetString().Should().Be("AI");
    }

    [Fact]
    public async Task OnlyMinimizedProfessionalDataAndTemporaryIdsAreTransferred()
    {
        using var rig = new Rig();
        var mentor = Profile();
        mentor.User.FullName = "Synthetic Private Person";
        mentor.User.Email = "private@example.test";
        mentor.Organization = "Synthetic Private Employer";
        mentor.CurrentAddress = "123 Synthetic Private Street";
        mentor.CvStorageUrl = "https://private.example.test/document";
        mentor.Experience = Evidence + "; Synthetic Private Person at Synthetic Private Employer.\n" +
            "Phone: 0901234567\nEmail: private@example.test\nDate of birth: 01/02/1990\nAddress: 123 Synthetic Private Street";
        await rig.Ranker.RankAsync([new(Guid.NewGuid(), "Marketing startup contact: project@example.test +84901234567")],
            [mentor], MentorRankingScope.AllSpecialties, default);
        var body = rig.Calls.Single().Body;
        foreach (var privateValue in new[] { mentor.User.FullName, mentor.User.Email, mentor.Organization,
                     mentor.CurrentAddress, mentor.CvStorageUrl, "0901234567", "01/02/1990", "project@example.test", FakeKey })
            body.Should().NotContain(privateValue!);
        body.Should().Contain("M001").And.Contain("P001");
    }

    [Fact]
    public async Task MatchingInputIsCachedForTenMinutesButChangedProfilesInvalidateTheCache()
    {
        using var rig = new Rig();
        var projects = new[] { new MentorRankingProject(Guid.NewGuid(), "Marketing startup") };
        var mentor = Profile();
        await rig.Ranker.RankAsync(projects, [mentor], MentorRankingScope.AllSpecialties, default);
        await rig.Ranker.RankAsync(projects, [mentor], MentorRankingScope.AllSpecialties, default);
        rig.Calls.Should().HaveCount(1);
        mentor.Experience += "; Product experimentation";
        await rig.Ranker.RankAsync(projects, [mentor], MentorRankingScope.AllSpecialties, default);
        rig.Calls.Should().HaveCount(2);
        rig.Clock.Now += TimeSpan.FromMinutes(11);
        await rig.Ranker.RankAsync(projects, [mentor], MentorRankingScope.AllSpecialties, default);
        rig.Calls.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("key")]
    [InlineData("transfer")]
    [InlineData("paid-model")]
    [InlineData("unicode-key")]
    public async Task InvalidConfigurationSendsNothing(string fault)
    {
        using var rig = new Rig();
        if (fault == "key") rig.Settings.ApiKey = "";
        if (fault == "unicode-key") rig.Settings.ApiKey = "invalid-\u0111";
        if (fault == "transfer") rig.Settings.DataTransferEnabled = false;
        if (fault == "paid-model") rig.Settings.Model = "gemini-unapproved-pro";
        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => Rank(rig));
        rig.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CancellationWhileWaitingForAnotherRequestDoesNotCallTheApiOrReleaseItsLock()
    {
        using var rig = new Rig();
        await rig.Coordinator.Gate.WaitAsync();
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Ranker.RankAsync(
                [new(Guid.NewGuid(), "Marketing startup")], [Profile()], MentorRankingScope.AllSpecialties, cancellation.Token));
            rig.Coordinator.Gate.CurrentCount.Should().Be(0);
            rig.Calls.Should().BeEmpty();
        }
        finally { rig.Coordinator.Gate.Release(); }
    }

    [Fact]
    public void DependencyInjectionUsesTheCurrentAiProviderAndBackendPolicy()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mentoring:MatchingProvider"] = "Gemini", ["Mentoring:Gemini:ApiKey"] = FakeKey,
            ["Mentoring:Gemini:DataTransferEnabled"] = "true"
        }).Build());
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGenerativeMentorFitRanker>().Should().BeOfType<GeminiMentorFitRanker>();
        provider.GetRequiredService<IMentorEvidenceGuard>().Should().BeOfType<FileMentorEvidenceGuard>();
        provider.GetRequiredService<IOptions<MentorScoringOptions>>().Value.ProfilePoints.Should().Be(70);
    }

    private static Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> Rank(Rig rig,
        string text = "Fictional marketing startup") => rig.Ranker.RankAsync([new(Guid.NewGuid(), text)],
        [Profile()], MentorRankingScope.AllSpecialties, default);

    private static MentorProfile Profile() => new()
    {
        Experience = Evidence,
        User = new User { FullName = "Synthetic mentor", Email = "synthetic@example.test" }
    };

    private static JsonObject Result(Call call)
    {
        using var request = JsonDocument.Parse(call.Body);
        using var input = JsonDocument.Parse(request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
        var firstMentor = input.RootElement.GetProperty("mentors")[0].GetProperty("id").GetString();
        var rows = new JsonArray();
        foreach (var project in input.RootElement.GetProperty("projects").EnumerateArray())
            rows.Add(new JsonObject { ["project"] = project.GetProperty("id").GetString(), ["mentors"] = new JsonArray
                { new JsonObject { ["id"] = firstMentor, ["criteria"] = Criteria() } } });
        return new JsonObject { ["results"] = rows };
    }

    private static HttpResponseMessage Success(Call call) => Envelope(Result(call).ToJsonString());
    private static JsonObject Criteria() => new()
    {
        ["experience"] = new JsonObject { ["score"] = 75, ["evidence"] = Evidence },
        ["expertise"] = new JsonObject { ["score"] = 0, ["evidence"] = "" },
        ["startupDomain"] = new JsonObject { ["score"] = 0, ["evidence"] = "" },
        ["technology"] = new JsonObject { ["score"] = 0, ["evidence"] = "" },
        ["bio"] = new JsonObject { ["score"] = 0, ["evidence"] = "" }
    };
    private static HttpResponseMessage Envelope(string json, string finish = "STOP") => Json(HttpStatusCode.OK, new
    {
        candidates = new[] { new { finishReason = finish, content = new { parts = new[] { new { text = json } } } } },
        usageMetadata = new { promptTokenCount = 100, candidatesTokenCount = 50, totalTokenCount = 150 }
    });
    private static HttpResponseMessage Error(HttpStatusCode status) => Json(status,
        new { error = new { message = "PRIVATE_PROVIDER_BODY " + FakeKey } });
    private static HttpResponseMessage Quota(string model, bool daily, bool shared = false, bool mixed = false)
    {
        var violations = new List<object> { new { quotaId = daily ? "RequestsPerModelPerDay" : "RequestsPerModelPerMinute",
            quotaDimensions = shared && !mixed ? new Dictionary<string, string>() : new Dictionary<string, string> { ["model"] = model } } };
        if (mixed) violations.Add(new { quotaId = "SharedProjectLimit", quotaDimensions = new Dictionary<string, string>() });
        return Json(HttpStatusCode.TooManyRequests, new { error = new { details = new[]
            { new Dictionary<string, object> { ["@type"] = "type.googleapis.com/google.rpc.QuotaFailure", ["violations"] = violations } } } });
    }
    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    private sealed record Call(string Model, string Body, Uri Uri, string Key);
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Rig : IDisposable
    {
        public List<Call> Calls { get; } = [];
        public GeminiMentorOptions Settings { get; } = new() { ApiKey = FakeKey, DataTransferEnabled = true, MinimumRequestIntervalSeconds = 0 };
        public GeminiRequestCoordinator Coordinator { get; } = new();
        public TestClock Clock { get; } = new();
        public GeminiMentorFitRanker Ranker { get; }
        private readonly HttpClient client;
        public Rig(Func<Call, HttpResponseMessage>? respond = null)
        {
            client = new HttpClient(new Handler(async (request, token) =>
            {
                var call = new Call(request.RequestUri!.AbsolutePath.Split('/').Last().Split(':')[0],
                    await request.Content!.ReadAsStringAsync(token), request.RequestUri, request.Headers.GetValues("x-goog-api-key").Single());
                Calls.Add(call);
                return (respond ?? Success)(call);
            }));
            Ranker = new(client, Options.Create(Settings), Coordinator, Clock, NullLogger<GeminiMentorFitRanker>.Instance);
        }
        public void Dispose() { client.Dispose(); Coordinator.Gate.Dispose(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
