using EHub.Application.Features.Admin.Mentors;
using EHub.Contracts.Mentors;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorBalanceSummaryBuilderTests
{
    [Theory]
    [InlineData(10, 3, 3, 4, 1)]
    [InlineData(12, 3, 4, 4, 0)]
    [InlineData(2, 5, 0, 1, 2)]
    [InlineData(0, 3, 0, 0, 0)]
    [InlineData(10, 0, 0, 0, 0)]
    public void TargetsIncludeRemaindersAndHandleEmptyOrOversizedPools(int teams, int count, int minimum, int maximum, int extras)
    {
        var mentors = Enumerable.Range(0, count).Select(index =>
            new AllocationMentor(Guid.NewGuid(), $"Synthetic mentor {index}", "", MentorType.Enterprise)).ToArray();

        var balance = MentorBalanceSummaryBuilder.Build(teams, mentors, [], AllocationStrategy.Ai)
            .Single(item => item.MentorType == nameof(MentorType.Enterprise));

        balance.SemesterTeamCount.Should().Be(teams);
        balance.EligibleMentorCount.Should().Be(count);
        balance.TargetMinTeams.Should().Be(minimum);
        balance.TargetMaxTeams.Should().Be(maximum);
        balance.ExtraTeamMentorCount.Should().Be(extras);
    }

    [Fact]
    public void PoolsRemainIndependentAndAiDoesNotCountTemporaryMentorsAsAutomaticCandidates()
    {
        var enterprise = Enumerable.Range(0, 3).Select(index =>
            new AllocationMentor(Guid.NewGuid(), $"Synthetic enterprise {index}", "", MentorType.Enterprise)).ToArray();
        var academic = new AllocationMentor(Guid.NewGuid(), "Synthetic academic", "", MentorType.Academic);
        var temporary = new AllocationMentor(Guid.NewGuid(), "Synthetic temporary", "", MentorType.Enterprise, true);
        var mentors = enterprise.Concat([academic, temporary]).ToArray();
        var loads = new[] { new MentorAllocationMentorLoad { MentorProfileId = enterprise[0].Id, TotalAfter = 7 } };

        var ai = MentorBalanceSummaryBuilder.Build(10, mentors, loads, AllocationStrategy.Ai);
        var business = ai.Single(item => item.MentorType == nameof(MentorType.Enterprise));
        business.EligibleMentorCount.Should().Be(3);
        business.MinLoadAfter.Should().Be(0, "eligible mentors without assignments participate in the balance");
        business.MaxLoadAfter.Should().Be(7, "fixed or manual workloads remain visible even above the target");
        ai.Single(item => item.MentorType == nameof(MentorType.Academic)).EligibleMentorCount.Should().Be(1);
        ai.Single(item => item.MentorType == nameof(MentorType.Academic)).TargetMaxTeams.Should().Be(10);
        MentorBalanceSummaryBuilder.Build(10, mentors, loads, AllocationStrategy.Balanced)
            .Single(item => item.MentorType == nameof(MentorType.Enterprise)).EligibleMentorCount.Should().Be(4);
    }
}
