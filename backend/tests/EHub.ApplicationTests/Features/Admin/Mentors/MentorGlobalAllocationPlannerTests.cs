using System.Diagnostics;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Admin.Mentors;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorGlobalAllocationPlannerTests
{
    [Fact]
    public void GlobalFitReservesTheScarceMentorForTheProjectThatNeedsThemMost()
    {
        var teams = Teams(2);
        var mentors = Mentors(2);
        var scores = Rankings(teams, mentors, (team, mentor) => new double[,] { { 90, 85 }, { 89, 20 } }[team, mentor]);

        var result = Allocate(teams, mentors, scores);

        result.Proposals.Single(item => item.Team.Id == teams[0].Id).Mentor.Id.Should().Be(mentors[1].Id);
        result.Proposals.Single(item => item.Team.Id == teams[1].Id).Mentor.Id.Should().Be(mentors[0].Id);
        TotalFit(result).Should().Be(174, "choosing 90 first would leave only 20 for the other project");
        result.Proposals.GroupBy(item => item.Mentor.Id).Should().OnlyContain(group => group.Count() == 1);
    }

    [Fact]
    public void SeededTiesAreRepeatableEvenWhenDatabaseResultsArriveInADifferentOrder()
    {
        var teams = Teams(17);
        var mentors = Mentors(5);
        var rankings = Rankings(teams, mentors, (_, _) => 70);

        var first = Allocate(teams, mentors, rankings, seed: 91);
        var reordered = Allocate(teams.Reverse().ToArray(), mentors.Reverse().ToArray(), rankings, seed: 91);

        Signature(first).Should().Equal(Signature(reordered));
        var differentSeeds = Enumerable.Range(1, 8).Select(seed => string.Join('|', Signature(Allocate(teams, mentors, rankings, seed: seed))));
        differentSeeds.Distinct().Count().Should().BeGreaterThan(1, "the seed may resolve genuinely equal fit choices");
    }

    [Fact]
    public void ProtectedOverloadStaysFixedWhileOtherMentorsReachTheMostEvenFeasibleLoads()
    {
        var teams = Teams(4);
        var mentors = Mentors(3);
        var existing = ExistingLoads(mentors, [9, 1, 0]);
        var rankings = Rankings(teams, mentors, (_, mentor) => mentor == 0 ? 100 : mentor == 1 ? 80 : 70);

        var result = Allocate(teams, mentors, rankings, existing);

        result.Proposals.Should().HaveCount(4).And.NotContain(item => item.Mentor.Id == mentors[0].Id);
        FinalLoads(result, mentors, existing).Order().Should().Equal(2, 3, 9);
        result.Proposals.Should().OnlyContain(item => item.ResultingLoad <= 3);
    }

    [Fact]
    public void ExistingAndManualSlotsStayProtectedAndOtherClassesContributeLoad()
    {
        var teams = Teams(4);
        var mentors = Mentors(3);
        var fixedSlots = new[]
        {
            new AllocationExistingAssignment(teams[0].Id, mentors[0].Id, MentorType.Enterprise),
            new AllocationExistingAssignment(teams[1].Id, mentors[1].Id, MentorType.Enterprise)
        }.Concat(ExistingLoads(mentors, [3, 0, 0])).ToArray();
        var rankings = Rankings(teams, mentors, (_, _) => 90);

        var result = Allocate(teams, mentors, rankings, fixedSlots);

        result.Proposals.Should().HaveCount(2).And.NotContain(item => item.Team.Id == teams[0].Id || item.Team.Id == teams[1].Id);
        result.Proposals.Should().NotContain(item => item.Mentor.Id == mentors[0].Id);
        FinalLoads(result, mentors, fixedSlots).Order().Should().Equal(1, 2, 4);
    }

    [Fact]
    public void ZeroEvidenceStillFillsEverySlotAtZeroAndRequiresAdminReview()
    {
        var teams = Teams(13);
        var mentors = Mentors(5);
        var rankings = Rankings(teams, mentors, (_, _) => 100, hasEvidence: false);

        var result = Allocate(teams, mentors, rankings);

        result.Proposals.Should().HaveCount(13).And.OnlyContain(item => item.Ranking != null &&
            item.Ranking.Score == 0 && item.Ranking.RankingValue == 0 && !item.Ranking.HasEvidence);
        result.Proposals.Should().OnlyContain(item => item.Ranking!.Reasons.Any(reason => reason.Contains("cần admin rà soát")));
        result.Warnings.Should().HaveCount(13);
        FinalLoads(result, mentors, []).Order().Should().Equal(2, 2, 3, 3, 3);
    }

    [Theory]
    [InlineData("missing-team")]
    [InlineData("missing-mentor")]
    [InlineData("nan")]
    [InlineData("negative")]
    [InlineData("too-large")]
    public void AnIncompleteOrInvalidProjectCannotBecomeAWorkloadFallback(string invalid)
    {
        var teams = Teams(3);
        var mentors = Mentors(2);
        var rankings = Rankings(teams, mentors, (_, _) => 75);
        var first = (Dictionary<Guid, MentorFitRanking>)rankings[teams[0].Id];
        if (invalid == "missing-team") rankings.Remove(teams[0].Id);
        else if (invalid == "missing-mentor") first.Remove(mentors[0].Id);
        else first[mentors[0].Id] = new MentorFitRanking(75, [], true,
            invalid == "nan" ? double.NaN : invalid == "negative" ? -0.1 : 100.1);

        var result = Allocate(teams, mentors, rankings);

        result.Proposals.Should().HaveCount(2).And.NotContain(item => item.Team.Id == teams[0].Id);
        result.Warnings.Should().ContainSingle().Which.Should().Contain("chưa có kết quả AI hợp lệ");
    }

    [Fact]
    public void FitComparisonRetainsUnroundedValues()
    {
        var teams = Teams(1);
        var mentors = Mentors(2);
        var rankings = Rankings(teams, mentors, (_, mentor) => mentor == 0 ? 80.0000001 : 80);

        var result = Allocate(teams, mentors, rankings);

        result.Proposals.Single().Mentor.Id.Should().Be(mentors[0].Id);
    }

    [Fact]
    public void WrongTypeAndTemporaryProfilesNeverEnterTheGlobalCandidateGraph()
    {
        var teams = Teams(3);
        var eligible = Mentors(2);
        var temporary = new AllocationMentor(Id(400), "Synthetic temporary", "", MentorType.Enterprise, true);
        var academic = new AllocationMentor(Id(401), "Synthetic academic", "", MentorType.Academic);
        var rankings = Rankings(teams, eligible, (_, _) => 75);

        var result = Allocate(teams, eligible.Concat([temporary, academic]).ToArray(), rankings);

        result.Proposals.Should().HaveCount(3).And.OnlyContain(item => eligible.Contains(item.Mentor));
    }

    [Fact]
    public void ExhaustiveSmallCasesConfirmBalanceFirstThenMaximumGlobalFit()
    {
        var random = new Random(117);
        for (var fixture = 0; fixture < 60; fixture++)
        {
            var teams = Teams(5);
            var mentors = Mentors(3);
            var before = mentors.Select(_ => random.Next(0, 4)).ToArray();
            var existing = ExistingLoads(mentors, before);
            var scores = new double[teams.Length, mentors.Length];
            for (var team = 0; team < teams.Length; team++)
                for (var mentor = 0; mentor < mentors.Length; mentor++) scores[team, mentor] = random.Next(0, 10001) / 100d;
            var rankings = Rankings(teams, mentors, (team, mentor) => scores[team, mentor]);
            var result = Allocate(teams, mentors, rankings, existing, fixture);
            var optimum = ExhaustiveOptimum(scores, before);

            FinalLoads(result, mentors, existing).Sum(load => load * load).Should().Be(optimum.Squares,
                $"fixture {fixture} must reach the most even feasible load distribution");
            TotalFit(result).Should().BeApproximately(optimum.Fit, 0.00000001,
                $"fixture {fixture} must maximize fit among all equally balanced assignments");
        }
    }

    [Fact]
    public void SyntheticEvaluationShowsTheOrderDependentGreedyGapWithoutClaimingSemanticAccuracy()
    {
        var teams = Teams(2);
        var mentors = Mentors(2);
        var values = new double[,] { { 90, 85 }, { 89, 20 } };
        var rankings = Rankings(teams, mentors, (team, mentor) => values[team, mentor]);
        var greedy = Enumerable.Range(1, 30).Select(seed => GreedyFixtureFit(values, seed)).ToArray();
        var optimized = Enumerable.Range(1, 30).Select(seed => TotalFit(Allocate(teams, mentors, rankings, seed: seed))).ToArray();

        greedy.Min().Should().Be(110);
        greedy.Max().Should().Be(174);
        optimized.Should().OnlyContain(score => score == 174);
    }

    [Fact]
    public void ThreeHundredProjectsAndSixtyMentorsProduceSixHundredTypedBalancedAssignments()
    {
        var teams = Teams(300);
        var business = Mentors(30);
        var technical = Enumerable.Range(0, 30).Select(index =>
            new AllocationMentor(Id(2000 + index), $"Synthetic technical {index}", "", MentorType.Academic)).ToArray();
        var mentors = business.Concat(technical).ToArray();
        var rankings = Rankings(teams, mentors, (team, mentor) => mentor % 30 == team % 30 ? 100 : 20);
        var timer = Stopwatch.StartNew();

        var result = MentorAllocationEngine.Allocate(teams, mentors, [], 81, AllocationStrategy.Ai, rankings);

        result.Proposals.Should().HaveCount(600);
        result.Proposals.Select(item => (item.Team.Id, item.Mentor.Type)).Should().OnlyHaveUniqueItems();
        result.Proposals.GroupBy(item => item.Mentor.Id).Should().HaveCount(60).And.OnlyContain(group => group.Count() == 10);
        TotalFit(result).Should().Be(60_000);
        timer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void CanceledAllocationStopsBeforeBuildingTheFlow()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var action = () => MentorAllocationEngine.Allocate([], [], [], 1, AllocationStrategy.Ai,
            cancellationToken: canceled.Token);
        action.Should().Throw<OperationCanceledException>();
    }

    private static AllocationOutcome Allocate(AllocationTeam[] teams, AllocationMentor[] mentors,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> rankings,
        AllocationExistingAssignment[]? existing = null, int seed = 17) =>
        MentorAllocationEngine.Allocate(teams, mentors,
            (existing ?? []).Concat(teams.Select(team => new AllocationExistingAssignment(team.Id, Guid.Empty, MentorType.Academic))).ToArray(),
            seed, AllocationStrategy.Ai, rankings);

    private static Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> Rankings(AllocationTeam[] teams,
        AllocationMentor[] mentors, Func<int, int, double> score, bool hasEvidence = true) =>
        teams.Select((team, index) => (team, index)).ToDictionary(item => item.team.Id, item =>
            (IReadOnlyDictionary<Guid, MentorFitRanking>)mentors.Select((mentor, index) => (mentor, index))
                .ToDictionary(candidate => candidate.mentor.Id, candidate => new MentorFitRanking(
                    (int)Math.Round(score(item.index, candidate.index)), ["Synthetic professional evidence"], hasEvidence,
                    score(item.index, candidate.index))));

    private static AllocationExistingAssignment[] ExistingLoads(AllocationMentor[] mentors, int[] loads) =>
        mentors.SelectMany((mentor, index) => Enumerable.Range(0, loads[index]).Select(offset =>
            new AllocationExistingAssignment(Id(10_000 + index * 100 + offset), mentor.Id, mentor.Type))).ToArray();

    private static int[] FinalLoads(AllocationOutcome result, AllocationMentor[] mentors, AllocationExistingAssignment[] existing) =>
        mentors.Select(mentor => existing.Count(item => item.MentorProfileId == mentor.Id) +
            result.Proposals.Count(item => item.Mentor.Id == mentor.Id)).ToArray();

    private static double TotalFit(AllocationOutcome result) => result.Proposals.Sum(item => item.Ranking!.RankingValue ?? item.Ranking.Score);
    private static string[] Signature(AllocationOutcome result) => result.Proposals
        .Select(item => $"{item.Team.Id}|{item.Mentor.Id}|{item.ResultingLoad}").ToArray();
    private static AllocationTeam[] Teams(int count) => Enumerable.Range(0, count)
        .Select(index => new AllocationTeam(Id(index + 1), Id(500), $"P{index}", $"Synthetic project {index}")).ToArray();
    private static AllocationMentor[] Mentors(int count) => Enumerable.Range(0, count)
        .Select(index => new AllocationMentor(Id(1000 + index), $"Synthetic business {index}", "", MentorType.Enterprise)).ToArray();
    private static Guid Id(int value) => new(value, 0, 0, new byte[8]);

    private static (int Squares, double Fit) ExhaustiveOptimum(double[,] scores, int[] before)
    {
        var bestSquares = int.MaxValue;
        var bestFit = double.NegativeInfinity;
        var loads = before.ToArray();
        void Visit(int project, double fit)
        {
            if (project == scores.GetLength(0))
            {
                var squares = loads.Sum(load => load * load);
                if (squares < bestSquares || squares == bestSquares && fit > bestFit)
                    (bestSquares, bestFit) = (squares, fit);
                return;
            }
            for (var mentor = 0; mentor < before.Length; mentor++)
            {
                loads[mentor]++;
                Visit(project + 1, fit + scores[project, mentor]);
                loads[mentor]--;
            }
        }
        Visit(0, 0);
        return (bestSquares, bestFit);
    }

    private static double GreedyFixtureFit(double[,] scores, int seed)
    {
        var random = new Random(seed);
        var loads = new int[scores.GetLength(1)];
        var total = 0d;
        foreach (var project in Enumerable.Range(0, scores.GetLength(0)).OrderBy(_ => random.Next()))
        {
            var minimum = loads.Min();
            var mentor = Enumerable.Range(0, loads.Length).Where(index => loads[index] == minimum)
                .OrderByDescending(index => scores[project, index]).First();
            loads[mentor]++;
            total += scores[project, mentor];
        }
        return total;
    }
}
