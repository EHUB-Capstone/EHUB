using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorRecommendationRankerTests
{
    [Fact]
    public async Task EmptyCandidatesDoNotCallTheModel()
    {
        await using var db = Context();
        var trained = Substitute.For<IMentorFitRanker>();
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();
        var result = await new MentorRecommendationRanker(db, embeddings, trained).RankAsync("Marketing", [], default);

        result.Should().BeEmpty();
        trained.DidNotReceiveWithAnyArgs().Rank(default!, default!, default);
        await embeddings.DidNotReceiveWithAnyArgs().SimilaritiesAsync(default!, default!, default);
    }

    [Fact]
    public async Task TrainedModelFailureDoesNotFallbackToEmbeddings()
    {
        await using var db = Context();
        var trained = Substitute.For<IMentorFitRanker>();
        trained.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<Guid, MentorFitRanking>>(_ => throw new MentorFitRankerUnavailableException());
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();
        var ranker = new MentorRecommendationRanker(db, embeddings, trained);

        var action = () => ranker.RankAsync("Marketing", [Profile()], default);

        await action.Should().ThrowAsync<MentorFitRankerUnavailableException>();
        await embeddings.DidNotReceiveWithAnyArgs().SimilaritiesAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteOrNonFiniteModelOutputIsUnavailable(bool invalidValue)
    {
        await using var db = Context();
        var mentor = Profile();
        var trained = Substitute.For<IMentorFitRanker>();
        trained.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(invalidValue
                ? new Dictionary<Guid, MentorFitRanking> { [mentor.Id] = new(80, [], true, double.NaN) }
                : new Dictionary<Guid, MentorFitRanking>());
        var ranker = new MentorRecommendationRanker(db, Substitute.For<IMentorEmbeddingSearch>(), trained);

        var action = () => ranker.RankAsync("Marketing", [mentor], default);

        await action.Should().ThrowAsync<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public async Task WeakEmbeddingAloneDoesNotBecomeEvidenceForAutomaticAssignment()
    {
        await using var db = Context();
        var mentor = Profile();
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();
        embeddings.SimilaritiesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, double> { [mentor.Id] = 0.1 });

        var result = await new MentorRecommendationRanker(db, embeddings).RankAsync("Marketing", [mentor], default);

        result[mentor.Id].HasEvidence.Should().BeFalse();
        result[mentor.Id].Reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task ExactProfessionalSkillIsEvidenceEvenWithoutHighSemanticSimilarity()
    {
        await using var db = Context();
        var mentor = Profile();
        mentor.Expertise = ["Marketing"];
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();
        embeddings.SimilaritiesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, double> { [mentor.Id] = 0.1 });

        var result = await new MentorRecommendationRanker(db, embeddings).RankAsync("Marketing", [mentor], default);

        result[mentor.Id].HasEvidence.Should().BeTrue();
        result[mentor.Id].Reasons.Should().Contain("Chuyên môn phù hợp: Marketing");
        result[mentor.Id].Reasons.Should().NotContain(reason => reason.StartsWith("Đang phụ trách "));
    }

    [Fact]
    public async Task HistoryFeedsTheTrainedModelWhileTheQuotingProviderReceivesOnlyThePublicProfile()
    {
        await using var db = Context();
        var mentor = Profile();
        mentor.Experience = "Public professional background";
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        db.MentoringSessions.Add(new MentoringSession
        {
            Title = "Marketing coaching", Notes = "PRIVATE_HISTORICAL_NOTES Marketing strategy",
            StartAt = new DateTimeOffset(now.AddDays(-2)), EndAt = new DateTimeOffset(now.AddDays(-2).AddHours(1)),
            Status = MentoringSessionStatus.Completed,
            MentorAssignment = new MentorAssignment
            {
                MentorProfile = mentor, AssignedAt = now.AddDays(-10), Status = MentorAssignmentStatus.Active,
                Team = new Team { TeamName = "Synthetic history", TeamCode = "HISTORY",
                    Class = new Class { ClassCode = "HISTORY", Course = new Course { Code = "HISTORY", Name = "Synthetic" },
                        Semester = new Semester { Code = "HISTORY", Name = "Synthetic", Year = 2026 } } }
            }
        });
        await db.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var trained = Substitute.For<IMentorFitRanker>();
        trained.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MentorFitRanking> { [mentor.Id] = new(80, ["Matching topic"]) });
        var semantic = Substitute.For<IMentorSemanticReranker>();
        semantic.RerankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
            Arg.Any<IReadOnlyDictionary<Guid, MentorFitRanking>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<IReadOnlyDictionary<Guid, MentorFitRanking>>(2)));

        var result = await new MentorRecommendationRanker(db, Substitute.For<IMentorEmbeddingSearch>(), trained, semantic, clock)
            .RankAsync("Marketing strategy", [mentor], default);

        trained.Received(1).Rank(Arg.Any<string>(), Arg.Is<IReadOnlyCollection<MentorProfile>>(profiles =>
            profiles != null && profiles.Single().Experience!.Contains("PRIVATE_HISTORICAL_NOTES")), Arg.Any<CancellationToken>());
        await semantic.Received(1).RerankAsync(Arg.Any<string>(), Arg.Is<IReadOnlyCollection<MentorProfile>>(profiles =>
            profiles != null && profiles.Single().Experience == "Public professional background"),
            Arg.Any<IReadOnlyDictionary<Guid, MentorFitRanking>>(), Arg.Any<CancellationToken>());
        result[mentor.Id].Reasons.Should().Contain(reason => reason.Contains("1 buổi hướng dẫn"));
        string.Join(' ', result[mentor.Id].Reasons).Should().NotContain("PRIVATE_HISTORICAL_NOTES");
        mentor.Experience.Should().Be("Public professional background");
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static MentorProfile Profile() => new() { User = new User { FullName = "Synthetic mentor", Email = "ranker@example.test" } };
}
