using EHub.Application.Features.Rankings.TeamRankings;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Rankings.TeamRankings;

public sealed class TeamRankingRulesTests
{
    [Fact]
    public void CourseTotal_UsesOnlyTeamComponentScoresAndConfiguredWeights()
    {
        var components = new[]
        {
            new TeamRankingComponent(25m, 8m, EvaluationStatus.Published),
            new TeamRankingComponent(35m, 7.5m, EvaluationStatus.Published),
            new TeamRankingComponent(40m, 9m, EvaluationStatus.Published),
        };

        TeamRankingRules.CalculateCourseTotal(components).Should().Be(8.23m);
        TeamRankingRules.ResolveStatus(components).Should().Be("PUBLISHED");
    }

    [Fact]
    public void MissingComponent_ContributesZeroToCurrentCourseTotal()
    {
        var components = new[]
        {
            new TeamRankingComponent(50m, 8m, EvaluationStatus.Published),
            new TeamRankingComponent(50m, null, null),
        };

        TeamRankingRules.CalculateCourseTotal(components).Should().Be(4m);
        TeamRankingRules.ResolveStatus(components).Should().Be("INCOMPLETE");
    }

    [Fact]
    public void NoGradedComponent_DoesNotProduceRankableCourseTotal()
    {
        var components = new[]
        {
            new TeamRankingComponent(50m, null, null),
            new TeamRankingComponent(50m, null, null),
        };

        TeamRankingRules.CalculateCourseTotal(components).Should().BeNull();
        TeamRankingRules.ResolveStatus(components).Should().Be("INCOMPLETE");
    }

    [Fact]
    public void SubmittedComponent_IsReadyButNotOfficiallyPublished()
    {
        var components = new[]
        {
            new TeamRankingComponent(50m, 8m, EvaluationStatus.Published),
            new TeamRankingComponent(50m, 9m, EvaluationStatus.Submitted),
        };

        TeamRankingRules.CalculateCourseTotal(components).Should().Be(8.5m);
        TeamRankingRules.ResolveStatus(components).Should().Be("READY_TO_PUBLISH");
    }
}
