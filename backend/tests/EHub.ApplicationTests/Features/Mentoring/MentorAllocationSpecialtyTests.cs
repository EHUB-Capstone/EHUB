using System.Net;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorAllocationSpecialtyTests
{
    [Theory]
    [InlineData(MentorType.Enterprise, "AI và computer vision")]
    [InlineData(MentorType.Academic, "Marketing và bán hàng")]
    public void AllocationUsesDocumentedSpecialtiesRegardlessOfMentorSlot(MentorType type, string specialty)
    {
        var mentor = new MentorProfile { Type = type, Experience = specialty };
        var ranker = Ranker();

        var recommendation = ranker.Rank(specialty, [mentor], default)[mentor.Id];
        var allocation = ranker.Rank(specialty, [mentor], MentorRankingScope.AllSpecialties, default)[mentor.Id];

        recommendation.HasEvidence.Should().BeFalse();
        recommendation.Score.Should().Be(0);
        allocation.HasEvidence.Should().BeTrue();
        allocation.Score.Should().BeGreaterThan(0);
        allocation.Reasons.Should().Contain(reason => reason.StartsWith("Kinh nghiệm có nội dung liên quan"));
        mentor.Type.Should().Be(type);
        mentor.MentorType.Should().Be(type == MentorType.Enterprise ? "Business" : "IT");
    }

    [Fact]
    public void DomainEvidenceCanSupportAllocationWithoutInventingAnItRecommendation()
    {
        var mentor = new MentorProfile { Type = MentorType.Academic, Experience = "Kinh nghiệm trong lĩnh vực giáo dục" };
        var ranker = Ranker();

        ranker.Rank("Dự án giáo dục", [mentor], default)[mentor.Id].HasEvidence.Should().BeFalse();
        ranker.Rank("Dự án giáo dục", [mentor], MentorRankingScope.AllSpecialties, default)[mentor.Id]
            .HasEvidence.Should().BeTrue();
        mentor.Type.Should().Be(MentorType.Academic);
    }

    [Theory]
    [InlineData("Không có kinh nghiệm AI và computer vision.", "AI và computer vision")]
    [InlineData("No experience with AI or computer vision.", "AI và computer vision")]
    [InlineData("AI và computer vision", "Không cần AI và computer vision; cần marketing.")]
    public void AllocationStillRespectsDeniedExperienceAndExcludedProjectNeeds(string experience, string project)
    {
        var mentor = new MentorProfile { Type = MentorType.Enterprise, Experience = experience, Expertise = ["AI"] };

        var result = Ranker().Rank(project, [mentor], MentorRankingScope.AllSpecialties, default)[mentor.Id];

        result.HasEvidence.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Theory]
    [InlineData("Đã phát triển ứng dụng giáo dục trên điện thoại.", "Dự án giáo dục cần hỗ trợ phụ huynh.", true)]
    [InlineData("Chỉ làm backend. Không có kinh nghiệm AI hoặc computer vision.", "Cần AI và computer vision để phân loại ảnh.", false)]
    public async Task SemanticAllocationUsesAllSpecialtiesAndKeepsExplicitDenials(string experience, string project,
        bool expectedEvidence)
    {
        var mentor = new MentorProfile { Type = MentorType.Academic, Experience = experience };
        var baseline = new Dictionary<Guid, MentorFitRanking> { [mentor.Id] = new(0, [], false, 0) };
        using var client = new HttpClient(new SemanticReply()) { BaseAddress = new Uri("http://127.0.0.1:8091/") };
        var semantic = new LocalSemanticMentorReranker(client, new ConfigurationBuilder().Build());

        var recommendation = await semantic.RerankAsync(project, [mentor], baseline, default);
        var allocation = await semantic.RerankAsync(project, [mentor], baseline, MentorRankingScope.AllSpecialties, default);

        recommendation[mentor.Id].HasEvidence.Should().BeFalse();
        allocation[mentor.Id].HasEvidence.Should().Be(expectedEvidence);
        mentor.Type.Should().Be(MentorType.Academic);
    }

    [Fact]
    public async Task AllocationFeedsHistoryToTheModelAndOnlyPublicEvidenceToTheQuotingProvider()
    {
        await using var db = Context();
        var mentor = new MentorProfile
        {
            Type = MentorType.Enterprise, Experience = "Public AI professional background",
            User = new User { FullName = "Synthetic mentor", Email = "allocation@example.test" }
        };
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        db.MentoringSessions.Add(new MentoringSession
        {
            Title = "AI coaching", Notes = "PRIVATE_SYNTHETIC_HISTORY AI classification",
            StartAt = new DateTimeOffset(now.AddDays(-2)), EndAt = new DateTimeOffset(now.AddDays(-2).AddHours(1)),
            Status = MentoringSessionStatus.Completed,
            MentorAssignment = new MentorAssignment
            {
                MentorProfile = mentor, AssignedAt = now.AddDays(-10), Status = MentorAssignmentStatus.Active,
                Team = new Team { TeamName = "Synthetic history", TeamCode = "ALLOCATION",
                    Class = new Class { ClassCode = "ALLOCATION", Course = new Course { Code = "ALLOCATION", Name = "Synthetic" },
                        Semester = new Semester { Code = "ALLOCATION", Name = "Synthetic", Year = 2026 } } }
            }
        });
        await db.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var trained = Substitute.For<IMentorFitRanker>();
        trained.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
                MentorRankingScope.AllSpecialties, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MentorFitRanking> { [mentor.Id] = new(80, ["Matching topic"]) });
        var semantic = Substitute.For<IMentorSemanticReranker>();
        semantic.RerankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
                Arg.Any<IReadOnlyDictionary<Guid, MentorFitRanking>>(), MentorRankingScope.AllSpecialties, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<IReadOnlyDictionary<Guid, MentorFitRanking>>(2)));

        var result = await new MentorRecommendationRanker(db, Substitute.For<IMentorEmbeddingSearch>(), trained, semantic, clock)
            .RankForAllocationAsync("AI classification", [mentor], default);

        trained.Received(1).Rank(Arg.Any<string>(), Arg.Is<IReadOnlyCollection<MentorProfile>>(profiles =>
                profiles != null && profiles.Single().Experience!.Contains("PRIVATE_SYNTHETIC_HISTORY")),
            MentorRankingScope.AllSpecialties, Arg.Any<CancellationToken>());
        await semantic.Received(1).RerankAsync(Arg.Any<string>(), Arg.Is<IReadOnlyCollection<MentorProfile>>(profiles =>
                profiles != null && profiles.Single().Experience == "Public AI professional background"),
            Arg.Any<IReadOnlyDictionary<Guid, MentorFitRanking>>(), MentorRankingScope.AllSpecialties, Arg.Any<CancellationToken>());
        result[mentor.Id].Reasons.Should().Contain(reason => reason.Contains("1 buổi hướng dẫn"));
        string.Join(' ', result[mentor.Id].Reasons).Should().NotContain("PRIVATE_SYNTHETIC_HISTORY");
        mentor.Experience.Should().Be("Public AI professional background");
        mentor.Type.Should().Be(MentorType.Enterprise);
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task AllocationDoesNotFallbackWhenTheConfiguredModelIsUnavailable()
    {
        await using var db = Context();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mentoring:TrainedModelPath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")
        }).Build();
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();
        var ranker = new MentorRecommendationRanker(db, embeddings, new FileMentorFitRanker(config));

        var action = () => ranker.RankForAllocationAsync("AI", [new MentorProfile()], default);

        await action.Should().ThrowAsync<MentorFitRankerUnavailableException>()
            .WithMessage("Mentor ranking model is unavailable.");
        await embeddings.DidNotReceiveWithAnyArgs().SimilaritiesAsync(default!, default!, default);
    }

    private static FileMentorFitRanker Ranker() => new(new ConfigurationBuilder().Build());

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class SemanticReply : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    schema = "ehub-mentor-semantic-v1", model = LocalSemanticMentorReranker.ModelId,
                    revision = LocalSemanticMentorReranker.ModelRevision,
                    items = new[] { new { id = "0", similarity = 0.9, evidenceIndex = 0 } }
                }), Encoding.UTF8, "application/json")
            });
    }
}
