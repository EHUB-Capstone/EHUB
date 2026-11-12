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
    public async Task EmptyCandidatesDoNotCallTheExternalModel()
    {
        await using var db = Context();
        var remote = Substitute.For<IGenerativeMentorFitRanker>();
        var ranker = new MentorRecommendationRanker(db, remote, Substitute.For<IMentorEvidenceGuard>());

        (await ranker.RankAsync("Marketing", [], default)).Should().BeEmpty();
        (await ranker.RankForAllocationBatchAsync([], [Profile()], default)).Should().BeEmpty();
        await remote.DidNotReceiveWithAnyArgs().RankAsync(default!, default!, default, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteOrNonFiniteCurrentModelOutputIsUnavailable(bool invalidValue)
    {
        await using var db = Context();
        var mentor = Profile();
        var remote = Substitute.For<IGenerativeMentorFitRanker>();
        remote.RankAsync(Arg.Any<IReadOnlyCollection<MentorRankingProject>>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
            Arg.Any<MentorRankingScope>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult<
                IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>>(call.ArgAt<IReadOnlyCollection<MentorRankingProject>>(0)
                .ToDictionary(project => project.Id, _ => (IReadOnlyDictionary<Guid, MentorFitRanking>)(invalidValue
                    ? new Dictionary<Guid, MentorFitRanking> { [mentor.Id] = new(80, [], true, double.NaN) }
                    : new Dictionary<Guid, MentorFitRanking>()))));
        var ranker = new MentorRecommendationRanker(db, remote, Substitute.For<IMentorEvidenceGuard>());

        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => ranker.RankAsync("Marketing", [mentor], default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerativeScoringKeepsHistoricalNotesLocalAndAppliesBonusesAfterOneRemoteCall(bool recommendation)
    {
        await using var db = Context();
        var mentor = Profile();
        mentor.Experience = "Public professional background";
        var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        db.MentoringSessions.Add(new MentoringSession
        {
            Title = "Marketing coaching", Notes = "PRIVATE_HISTORY_NOT_FOR_GEMINI",
            StartAt = new DateTimeOffset(now.AddDays(-2)), EndAt = new DateTimeOffset(now.AddDays(-2).AddHours(1)),
            Status = MentoringSessionStatus.Completed,
            MentorAssignment = new MentorAssignment
            {
                MentorProfile = mentor, AssignedAt = now.AddDays(-10), Status = MentorAssignmentStatus.Active,
                Team = new Team { TeamName = "Synthetic history", TeamCode = "REMOTE-HISTORY",
                    Class = new Class { ClassCode = "REMOTE-HISTORY", Course = new Course { Code = "REMOTE", Name = "Synthetic" },
                        Semester = new Semester { Code = "REMOTE", Name = "Synthetic", Year = 2026 } } }
            }
        });
        await db.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var projects = recommendation ? new[] { new MentorRankingProject(Guid.Empty, "Marketing strategy") } :
            [new MentorRankingProject(Guid.NewGuid(), "Marketing strategy"), new MentorRankingProject(Guid.NewGuid(), "Customer acquisition")];
        var scope = recommendation ? MentorRankingScope.MentorRole : MentorRankingScope.AllSpecialties;
        var remote = Substitute.For<IGenerativeMentorFitRanker>();
        var guard = Substitute.For<IMentorEvidenceGuard>();
        guard.FindContradictions(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
        remote.RankAsync(Arg.Any<IReadOnlyCollection<MentorRankingProject>>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
                Arg.Any<MentorRankingScope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>>(projects.ToDictionary(
                project => project.Id, _ => (IReadOnlyDictionary<Guid, MentorFitRanking>)new Dictionary<Guid, MentorFitRanking>
                    { [mentor.Id] = new(80, ["Public professional background"]) { Criteria = new(100, 100, 100, 100, 100) } })));
        var ranker = new MentorRecommendationRanker(db, remote, guard, clock);

        var results = recommendation
            ? new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>
                { [Guid.Empty] = await ranker.RankAsync(projects[0].Text, [mentor], default) }
            : await ranker.RankForAllocationBatchAsync(projects, [mentor], default);

        results.Should().HaveCount(projects.Length);
        results.Values.Should().OnlyContain(scores => scores[mentor.Id].Score == 73);
        guard.Received(projects.Length).FindContradictions(Arg.Any<string>(), Arg.Is<IReadOnlyCollection<MentorProfile>>(items =>
            items != null && items.Single().Experience == "Public professional background"), Arg.Any<CancellationToken>());
        await remote.Received(1).RankAsync(Arg.Is<IReadOnlyCollection<MentorRankingProject>>(items => items != null && items.Count == projects.Length),
            Arg.Is<IReadOnlyCollection<MentorProfile>>(items => items != null && items.Single().Experience == "Public professional background"),
            scope, Arg.Any<CancellationToken>());
        results.Values.SelectMany(scores => scores.Values).SelectMany(fit => fit.Reasons)
            .Should().NotContain(reason => reason.Contains("PRIVATE_HISTORY"));
        mentor.Experience.Should().Be("Public professional background");
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task GenerativeBatchCannotOmitAProjectOrFallbackToAnotherProvider()
    {
        await using var db = Context();
        var remote = Substitute.For<IGenerativeMentorFitRanker>();
        remote.RankAsync(Arg.Any<IReadOnlyCollection<MentorRankingProject>>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
                Arg.Any<MentorRankingScope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>>(
                new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>()));
        var ranker = new MentorRecommendationRanker(db, remote, Substitute.For<IMentorEvidenceGuard>());

        await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => ranker.RankForAllocationBatchAsync(
            [new(Guid.NewGuid(), "Marketing strategy")], [Profile()], default));

    }

    [Fact]
    public async Task GenerativeRequestsSeparateRolesAndOnlyIncludeProjectsMissingThatSlot()
    {
        await using var db = Context();
        var business = Profile();
        var technical = Profile();
        technical.Type = MentorType.Academic;
        var both = new MentorRankingProject(Guid.NewGuid(), "Marketing and React");
        var onlyIt = new MentorRankingProject(Guid.NewGuid(), "Marketing and React") { RequiredMentorTypes = [MentorType.Academic] };
        var full = new MentorRankingProject(Guid.NewGuid(), "Already fully assigned") { RequiredMentorTypes = [] };
        var remote = Substitute.For<IGenerativeMentorFitRanker>();
        remote.RankAsync(Arg.Any<IReadOnlyCollection<MentorRankingProject>>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
                Arg.Any<MentorRankingScope>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>>(
                call.ArgAt<IReadOnlyCollection<MentorRankingProject>>(0).ToDictionary(project => project.Id,
                    _ => (IReadOnlyDictionary<Guid, MentorFitRanking>)call.ArgAt<IReadOnlyCollection<MentorProfile>>(1)
                        .ToDictionary(profile => profile.Id, _ => new MentorFitRanking(80, []) { Criteria = new(80, 80, 80, 80, 80) }))));
        var guard = Substitute.For<IMentorEvidenceGuard>();
        guard.FindContradictions(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
        var ranker = new MentorRecommendationRanker(db, remote, guard);

        var result = await ranker.RankForAllocationBatchAsync([both, onlyIt, full], [business, technical], default);

        result[both.Id].Keys.Should().BeEquivalentTo([business.Id, technical.Id]);
        result[onlyIt.Id].Keys.Should().BeEquivalentTo([technical.Id]);
        result[full.Id].Should().BeEmpty();
        await remote.Received(1).RankAsync(Arg.Is<IReadOnlyCollection<MentorRankingProject>>(items => items != null && items.Count == 1 && items.Single().Id == both.Id),
            Arg.Is<IReadOnlyCollection<MentorProfile>>(items => items != null && items.Count == 1 && items.Single().Type == MentorType.Enterprise), MentorRankingScope.AllSpecialties, Arg.Any<CancellationToken>());
        await remote.Received(1).RankAsync(Arg.Is<IReadOnlyCollection<MentorRankingProject>>(items => items != null && items.Count == 2 && items.All(p => p.Id != full.Id)),
            Arg.Is<IReadOnlyCollection<MentorProfile>>(items => items != null && items.Count == 1 && items.Single().Type == MentorType.Academic), MentorRankingScope.AllSpecialties, Arg.Any<CancellationToken>());
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static MentorProfile Profile() => new() { User = new User { FullName = "Synthetic mentor", Email = "ranker@example.test" } };
}
