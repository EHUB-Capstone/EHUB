using EHub.Application.Features.Admin.Mentors;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorAllocationEngineTests
{
    [Fact]
    public void Allocate_FillsBothSlotsWithMatchingMentorTypeOnly()
    {
        var teams = Teams(6);
        var mentors = Mentors(MentorType.Enterprise, 3).Concat(Mentors(MentorType.Academic, 2)).ToArray();

        var outcome = MentorAllocationEngine.Allocate(teams, mentors, [], seed: 1);

        outcome.Warnings.Should().BeEmpty();
        outcome.Proposals.Should().HaveCount(12);
        foreach (var slot in new[] { MentorType.Enterprise, MentorType.Academic })
        {
            var forSlot = outcome.Proposals.Where(item => item.Mentor.Type == slot).ToArray();
            forSlot.Select(item => item.Team.Id).Should().BeEquivalentTo(teams.Select(team => team.Id));
        }
    }

    [Fact]
    public void Allocate_KeepsLoadsWithinOneOfEachOtherPerMentorType()
    {
        var teams = Teams(107);
        var mentors = Mentors(MentorType.Enterprise, 37).Concat(Mentors(MentorType.Academic, 17)).ToArray();

        var outcome = MentorAllocationEngine.Allocate(teams, mentors, [], seed: 7);

        foreach (var slot in new[] { MentorType.Enterprise, MentorType.Academic })
        {
            var counts = outcome.Proposals
                .Where(item => item.Mentor.Type == slot)
                .GroupBy(item => item.Mentor.Id)
                .Select(group => group.Count())
                .ToArray();
            (counts.Max() - counts.Min()).Should().BeLessThanOrEqualTo(1);
        }

        outcome.Proposals.Count(item => item.Mentor.Type == MentorType.Enterprise).Should().Be(107);
        outcome.Proposals.Count(item => item.Mentor.Type == MentorType.Academic).Should().Be(107);
    }

    [Fact]
    public void Allocate_NeverTouchesSlotsThatAlreadyHaveAMentor()
    {
        var teams = Teams(3);
        var enterprise = Mentors(MentorType.Enterprise, 2);
        var academic = Mentors(MentorType.Academic, 2);
        var existing = new[]
        {
            new AllocationExistingAssignment(teams[0].Id, enterprise[0].Id, MentorType.Enterprise),
            new AllocationExistingAssignment(teams[0].Id, academic[0].Id, MentorType.Academic),
            new AllocationExistingAssignment(teams[1].Id, enterprise[1].Id, MentorType.Enterprise)
        };

        var outcome = MentorAllocationEngine.Allocate(teams, enterprise.Concat(academic).ToArray(), existing, seed: 3);

        outcome.Proposals.Should().NotContain(item => item.Team.Id == teams[0].Id);
        outcome.Proposals.Should().NotContain(item => item.Team.Id == teams[1].Id && item.Mentor.Type == MentorType.Enterprise);
        outcome.Proposals.Should().ContainSingle(item => item.Team.Id == teams[1].Id && item.Mentor.Type == MentorType.Academic);
        outcome.Proposals.Count(item => item.Team.Id == teams[2].Id).Should().Be(2);
    }

    [Fact]
    public void Allocate_CountsExistingAssignmentsAsMentorLoad()
    {
        var teams = Teams(4);
        var mentors = Mentors(MentorType.Enterprise, 2);
        var busyMentor = mentors[0];
        var existing = new[]
        {
            new AllocationExistingAssignment(teams[0].Id, busyMentor.Id, MentorType.Enterprise),
            new AllocationExistingAssignment(teams[1].Id, busyMentor.Id, MentorType.Enterprise)
        };

        var outcome = MentorAllocationEngine.Allocate(teams, mentors, existing, seed: 5);

        outcome.Proposals.Should().HaveCount(2);
        outcome.Proposals.Should().OnlyContain(item => item.Mentor.Id == mentors[1].Id);
        outcome.Proposals.Select(item => item.ResultingLoad).Should().Equal(1, 2);
    }

    [Fact]
    public void Allocate_WarnsAndSkipsWhenNoMentorOfTheNeededTypeExists()
    {
        var teams = Teams(2);
        var mentors = Mentors(MentorType.Enterprise, 1);

        var outcome = MentorAllocationEngine.Allocate(teams, mentors, [], seed: 2);

        outcome.Warnings.Should().ContainSingle().Which.Should().Contain("Academic").And.Contain("2");
        outcome.Proposals.Should().OnlyContain(item => item.Mentor.Type == MentorType.Enterprise);
    }

    [Fact]
    public void Allocate_ProducesNoWarningWhenNothingIsMissingEvenWithoutMentors()
    {
        var teams = Teams(1);
        var enterprise = Mentors(MentorType.Enterprise, 1);
        var academic = Mentors(MentorType.Academic, 1);
        var existing = new[]
        {
            new AllocationExistingAssignment(teams[0].Id, enterprise[0].Id, MentorType.Enterprise),
            new AllocationExistingAssignment(teams[0].Id, academic[0].Id, MentorType.Academic)
        };

        var outcome = MentorAllocationEngine.Allocate(teams, [], existing, seed: 1);

        outcome.Proposals.Should().BeEmpty();
        outcome.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Allocate_IsDeterministicForTheSameSeed()
    {
        var teams = Teams(40);
        var mentors = Mentors(MentorType.Enterprise, 9).Concat(Mentors(MentorType.Academic, 5)).ToArray();

        var first = MentorAllocationEngine.Allocate(teams, mentors, [], seed: 99);
        var second = MentorAllocationEngine.Allocate(teams, mentors, [], seed: 99);

        Signature(first).Should().Equal(Signature(second));
    }

    [Fact]
    public void Allocate_RandomStrategy_StillRespectsTheHardRules()
    {
        var teams = Teams(20);
        var enterprise = Mentors(MentorType.Enterprise, 4);
        var academic = Mentors(MentorType.Academic, 3);
        var existing = new[] { new AllocationExistingAssignment(teams[0].Id, enterprise[0].Id, MentorType.Enterprise) };

        var outcome = MentorAllocationEngine.Allocate(teams, enterprise.Concat(academic).ToArray(), existing, seed: 11, AllocationStrategy.Random);

        outcome.Proposals.Should().HaveCount(39);
        outcome.Proposals.Should().NotContain(item => item.Team.Id == teams[0].Id && item.Mentor.Type == MentorType.Enterprise);
        outcome.Proposals.Select(item => (item.Team.Id, item.Mentor.Type)).Should().OnlyHaveUniqueItems();
        outcome.Proposals.Where(item => item.Mentor.Type == MentorType.Enterprise).Should().OnlyContain(item => enterprise.Contains(item.Mentor));
        outcome.Proposals.Where(item => item.Mentor.Type == MentorType.Academic).Should().OnlyContain(item => academic.Contains(item.Mentor));
    }

    [Fact]
    public void Allocate_RandomStrategy_IsDeterministicForTheSameSeed()
    {
        var teams = Teams(30);
        var mentors = Mentors(MentorType.Enterprise, 6).Concat(Mentors(MentorType.Academic, 4)).ToArray();

        var first = MentorAllocationEngine.Allocate(teams, mentors, [], seed: 5, AllocationStrategy.Random);
        var second = MentorAllocationEngine.Allocate(teams, mentors, [], seed: 5, AllocationStrategy.Random);

        Signature(first).Should().Equal(Signature(second));
    }

    [Fact]
    public void Allocate_RandomStrategy_IgnoresTheCurrentLoad()
    {
        var teams = Teams(10);
        var mentors = Mentors(MentorType.Enterprise, 3);
        var busy = mentors[0];
        var existing = teams.Take(5).Select(team => new AllocationExistingAssignment(team.Id, busy.Id, MentorType.Enterprise)).ToArray();

        var pickedTheBusyMentor = Enumerable.Range(1, 40)
            .Select(seed => MentorAllocationEngine.Allocate(teams, mentors, existing, seed, AllocationStrategy.Random))
            .Any(outcome => outcome.Proposals.Any(item => item.Mentor.Id == busy.Id));
        var balancedNeverDoes = Enumerable.Range(1, 40)
            .Select(seed => MentorAllocationEngine.Allocate(teams, mentors, existing, seed, AllocationStrategy.Balanced))
            .All(outcome => outcome.Proposals.All(item => item.Mentor.Id != busy.Id));

        pickedTheBusyMentor.Should().BeTrue("Random does not look at the load");
        balancedNeverDoes.Should().BeTrue("the busy mentor already carries more teams than the others will reach");
    }

    [Fact]
    public void Allocate_RandomStrategy_WarnsWhenNoMentorOfATypeExists()
    {
        var outcome = MentorAllocationEngine.Allocate(Teams(2), Mentors(MentorType.Academic, 1), [], seed: 2, AllocationStrategy.Random);

        outcome.Warnings.Should().ContainSingle().Which.Should().Contain("Enterprise");
        outcome.Proposals.Should().HaveCount(2).And.OnlyContain(item => item.Mentor.Type == MentorType.Academic);
    }

    // Characterization: the engine must keep producing exactly what the original inline algorithm in
    // MentorAdminHandler.PreviewAllocationAsync produced for the same seed. Remove LegacyAllocate once the
    // selection strategies are replaced in a later phase.
    [Theory]
    [InlineData(1)]
    [InlineData(12345)]
    [InlineData(2026)]
    [InlineData(int.MaxValue - 1)]
    public void Allocate_MatchesLegacyInlineAlgorithmForTheSameSeed(int seed)
    {
        var teams = Teams(31);
        var enterprise = Mentors(MentorType.Enterprise, 7);
        var academic = Mentors(MentorType.Academic, 4);
        var mentors = enterprise.Concat(academic).ToArray();
        var existing = new[]
        {
            new AllocationExistingAssignment(teams[0].Id, enterprise[0].Id, MentorType.Enterprise),
            new AllocationExistingAssignment(teams[1].Id, enterprise[0].Id, MentorType.Enterprise),
            new AllocationExistingAssignment(teams[2].Id, academic[1].Id, MentorType.Academic)
        };

        var actual = MentorAllocationEngine.Allocate(teams, mentors, existing, seed);
        var expected = LegacyAllocate(teams, mentors, existing, seed);

        Signature(actual).Should().Equal(expected);
    }

    private static string[] Signature(AllocationOutcome outcome) =>
        outcome.Proposals.Select(item => $"{item.Team.Id}|{item.Mentor.Id}|{item.Mentor.Type}|{item.ResultingLoad}").ToArray();

    private static string[] LegacyAllocate(
        AllocationTeam[] teams,
        AllocationMentor[] mentors,
        AllocationExistingAssignment[] active,
        int seed)
    {
        var random = new Random(seed);
        var result = new List<string>();
        foreach (var type in new[] { MentorType.Enterprise, MentorType.Academic })
        {
            var pool = mentors.Where(item => item.Type == type).ToArray();
            var missing = teams.Where(team => active.All(item => item.TeamId != team.Id || item.Slot != type)).OrderBy(_ => random.Next()).ToArray();
            if (missing.Length > 0 && pool.Length == 0) continue;
            var loads = pool.ToDictionary(item => item.Id, item => active.Count(assignment => assignment.MentorProfileId == item.Id));
            foreach (var team in missing)
            {
                var minimum = loads.Values.Min();
                var eligible = pool.Where(item => loads[item.Id] == minimum).OrderBy(_ => random.Next()).ToArray();
                var mentor = eligible[0];
                loads[mentor.Id]++;
                result.Add($"{team.Id}|{mentor.Id}|{type}|{loads[mentor.Id]}");
            }
        }

        return result.ToArray();
    }

    private static AllocationTeam[] Teams(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), $"T{index:000}", $"Team {index}"))
            .ToArray();

    private static AllocationMentor[] Mentors(MentorType type, int count) =>
        Enumerable.Range(1, count)
            .Select(index => new AllocationMentor(Guid.NewGuid(), $"{type} {index}", $"{type}{index}@example.com".ToLowerInvariant(), type))
            .ToArray();
}
