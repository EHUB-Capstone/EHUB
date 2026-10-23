using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorTrainedRankerTests
{
    [Fact]
    public void OfflineTrainerAndRuntimeProduceTheSameFeatureVectors()
    {
        using var stream = typeof(FileMentorFitRanker).Assembly
            .GetManifestResourceStream("EHub.Mentoring.BootstrapModel.json");
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        var model = MentorRankingModel.FromJson(reader.ReadToEnd());
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Mentoring", "parity-fixtures.json");
        using var fixtures = JsonDocument.Parse(File.ReadAllText(fixturePath));
        fixtures.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var fixture in fixtures.RootElement.EnumerateArray())
        {
            var mentor = JsonSerializer.Deserialize<MentorProfile>(fixture.GetProperty("mentor").GetRawText(), options)!;
            var actual = MentorRankingFeatures.Extract(model, fixture.GetProperty("projectText").GetString()!, mentor);
            var expected = fixture.GetProperty("features").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            actual.Length.Should().Be(expected.Length);
            for (var i = 0; i < actual.Length; i++) actual[i].Should().BeApproximately(expected[i], 1e-12);
        }
    }

    [Fact]
    public void EmbeddedBootstrapArtifactRunsOnCpuWithoutAnEmbeddingService()
    {
        var ranker = new FileMentorFitRanker(new ConfigurationBuilder().Build());
        var experienced = new MentorProfile { Experience = "Marketing" };
        var bioOnly = new MentorProfile { Bio = "Marketing" };
        var unrelated = new MentorProfile { Expertise = ["PostgreSQL"] };

        var result = ranker.Rank("Marketing", [experienced, bioOnly, unrelated], default);

        result[experienced.Id].Score.Should().BeInRange(1, 100);
        result[experienced.Id].Score.Should().BeGreaterThan(result[bioOnly.Id].Score);
        result[unrelated.Id].Score.Should().Be(0);
        result[experienced.Id].Reasons.Should().Contain(x => x.Contains("chưa được kiểm chứng"));
    }

    [Fact]
    public void FeatureValuesFollowThePortableSchema()
    {
        var mentor = new MentorProfile
        {
            Experience = "marketing", Expertise = ["operations"], Bio = "marketing"
        };

        var features = MentorRankingFeatures.Extract(Model(), "marketing operations", mentor);

        features.Should().Equal(0.5, 0.5, 0, 0, 0.5, 0.5, 1, 0.25);
    }

    [Fact]
    public void RelevantExperienceRanksAboveBioAndUnrelatedProfiles()
    {
        var experienced = new MentorProfile { Experiences = [new() { Area = "Marketing" }] };
        var bioOnly = new MentorProfile { Bio = "Marketing" };
        var unrelated = new MentorProfile { Expertise = ["C#"] };

        var result = new TrainedMentorFitScorer(Model()).Rank("Cần tiếp thị cho dự án",
            [experienced, bioOnly, unrelated], default);

        result[experienced.Id].Score.Should().BeGreaterThan(result[bioOnly.Id].Score);
        result[bioOnly.Id].Score.Should().BeGreaterThan(result[unrelated.Id].Score);
        result[experienced.Id].Reasons.Should().Contain("Kinh nghiệm có nội dung liên quan: marketing");
        result[experienced.Id].Reasons.Should().Contain(x => x.Contains("chưa được kiểm chứng"));
    }

    [Theory]
    [InlineData("C#", "C# API", 1)]
    [InlineData("C++", "C++ application", 1)]
    [InlineData("C#", "C++ application", 0)]
    [InlineData("Java", "JavaScript application", 0)]
    [InlineData(".NET", "ASP.NET application", 0)]
    [InlineData("Java", "Java.", 1)]
    [InlineData("C#", "C#.", 1)]
    public void SymbolSkillsAndWholeTermsRemainDistinct(string skill, string project, double expected)
    {
        var features = MentorRankingFeatures.Extract(Model(), project,
            new MentorProfile { TechnologySkills = [skill] });

        features[3].Should().Be(expected);
    }

    [Fact]
    public void VietnameseAccentsCaseAndWhitespaceHaveTheSameMeaning()
    {
        var mentor = new MentorProfile { Experience = "ĐỊNH   GIÁ" };

        var accented = MentorRankingFeatures.Extract(Model(), "Cần định giá", mentor);
        var unaccented = MentorRankingFeatures.Extract(Model(), "can dinh gia", mentor);

        accented.Should().Equal(unaccented);
        accented[0].Should().Be(1);
    }

    [Fact]
    public void EmptyOrUnknownEvidenceDoesNotProduceASyntheticHighScore()
    {
        var mentor = new MentorProfile { Expertise = ["Marketing"] };
        var scorer = new TrainedMentorFitScorer(Model(bias: 50));

        var result = scorer.Rank("unknown-field", [mentor], default)[mentor.Id];

        result.Score.Should().Be(0);
        result.Reasons.Should().Contain("Chưa tìm thấy bằng chứng chuyên môn phù hợp trong hồ sơ");
        scorer.Rank("", [mentor], default)[mentor.Id].Score.Should().Be(0);
    }

    [Fact]
    public void IdentityTypeEmployerAndYearsDoNotChangeFeatures()
    {
        var first = new MentorProfile
        {
            UserId = Guid.NewGuid(), Organization = "Marketing", Type = MentorType.Enterprise,
            Experiences = [new() { Area = "Marketing", Years = 1 }]
        };
        var second = new MentorProfile
        {
            UserId = Guid.NewGuid(), Organization = "Unknown", Type = MentorType.Academic,
            Experiences = [new() { Area = "Marketing", Years = 30 }]
        };

        MentorRankingFeatures.Extract(Model(), "Marketing", first)
            .Should().Equal(MentorRankingFeatures.Extract(Model(), "Marketing", second));
    }

    [Fact]
    public void InputClippingBoundsEvidenceAndAvoidsMalformedSurrogateFailures()
    {
        var mentor = new MentorProfile
        {
            Experience = new string('x', 6000) + " marketing", Bio = "\ud800",
            Expertise = Enumerable.Repeat("unknown", 64).Append("marketing").ToArray()
        };

        var features = MentorRankingFeatures.Extract(Model(), "marketing", mentor);

        features.Take(7).Should().OnlyContain(x => x == 0);
    }

    [Fact]
    public void CancelledRequestsAreNotTurnedIntoModelErrors()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var scorer = new TrainedMentorFitScorer(Model());

        var act = () => scorer.Rank("marketing", [], cancellation.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Theory]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"weights\":[NaN]}")]
    [InlineData("null")]
    public void InvalidArtifactsAreRejected(string json)
    {
        var act = () => MentorRankingModel.FromJson(json);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void FeatureSchemaAndCoefficientBoundsAreEnforced()
    {
        var schemaMismatch = Artifact().Replace("ehub-mentor-fit-v2", "unknown-schema", StringComparison.Ordinal);
        var excessiveWeight = Artifact(weights: [101, 3, 1, 1, 0.5, 2, 1, 0]);
        var negativeEvidenceWeight = Artifact(weights: [-1, 3, 1, 1, 0.5, 2, 1, 0]);
        var infiniteWeight = Artifact().Replace("\"bias\":-4", "\"bias\":1e999", StringComparison.Ordinal);

        foreach (var json in new[] { schemaMismatch, excessiveWeight, negativeEvidenceWeight, infiniteWeight })
        {
            var act = () => MentorRankingModel.FromJson(json);
            act.Should().Throw<InvalidDataException>();
        }
    }

    [Fact]
    public void DuplicateNormalizedAliasesAndFeatureNamesAreRejected()
    {
        var duplicateAlias = Artifact().Replace("[\"operations\",", "[\"marketing\",", StringComparison.Ordinal);
        var duplicateFeature = Artifact().Replace("expertise_topic_coverage", "experience_topic_coverage", StringComparison.Ordinal);

        foreach (var json in new[] { duplicateAlias, duplicateFeature })
        {
            var act = () => MentorRankingModel.FromJson(json);
            act.Should().Throw<InvalidDataException>();
        }
    }

    [Fact]
    public void SyntheticTrainingCannotClaimValidatedSuitability()
    {
        var artifact = Artifact().Replace("\"suitabilityValidated\":false", "\"suitabilityValidated\":true",
            StringComparison.Ordinal);

        var act = () => MentorRankingModel.FromJson(artifact);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void MissingConfiguredArtifactIsExplicitlyUnavailable()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mentoring:TrainedModelPath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")
        }).Build();
        var ranker = new FileMentorFitRanker(config);

        var act = () => ranker.Rank("Marketing", [], default);

        act.Should().Throw<MentorFitRankerUnavailableException>()
            .WithMessage("Mentor ranking model is unavailable.");
    }

    private static MentorRankingModel Model(double bias = -4) => MentorRankingModel.FromJson(Artifact(bias: bias));

    private static string Artifact(double[]? weights = null, double bias = -4) => JsonSerializer.Serialize(new
    {
        version = 1, featureSchema = MentorRankingModel.Schema,
        trainingSource = "synthetic-bootstrap", suitabilityValidated = false,
        featureNames = MentorRankingModel.FeatureNames,
        weights = weights ?? [4, 3, 1, 1, 0.5, 2, 1, 0], bias,
        topics = new[]
        {
            new { id = "marketing", aliases = new[] { "marketing", "tiếp thị" } },
            new { id = "operations", aliases = new[] { "operations", "vận hành" } },
            new { id = "pricing", aliases = new[] { "định giá" } },
            new { id = "csharp", aliases = new[] { "C#" } },
            new { id = "cpp", aliases = new[] { "C++" } },
            new { id = "java", aliases = new[] { "Java" } },
            new { id = "dotnet", aliases = new[] { ".NET" } }
        },
        stopWords = new[] { "can", "cho", "du", "an" }
    });
}
