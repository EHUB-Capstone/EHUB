using EHub.Application.Features.Classes.GroupProjectConsistency;
using EHub.Contracts.Classes;
using FluentAssertions;

namespace EHub.UnitTests.Common;

public sealed class GroupProjectConsistencyRulesTests
{
    [Fact]
    public void Evaluate_ShouldBeConsistent_WhenGroupsAndProjectsAreOneToOne()
    {
        var warnings = GroupProjectConsistencyRules.Evaluate(
        [
            ("G01", "Project A"),
            ("G01", "Project A"),
            ("G02", "Project B"),
            ("G02", "Project B")
        ]);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldWarn_WhenOneGroupHasMultipleProjects()
    {
        var warnings = GroupProjectConsistencyRules.Evaluate(
        [
            ("G01", "Project A"),
            ("G01", "Project A"),
            ("G01", "Project B")
        ]);

        var warning = warnings.Should().ContainSingle(item =>
            item.Type == GroupProjectWarningTypes.GroupHasMultipleProjects).Subject;
        warning.Subject.Should().Be("G01");
        warning.Related.Should().Equal("Project A", "Project B");
        warning.Message.Should().Be("Group `G01` is assigned to multiple projects: `Project A`, `Project B`.");
    }

    [Fact]
    public void Evaluate_ShouldWarn_WhenOneProjectHasMultipleGroups()
    {
        var warnings = GroupProjectConsistencyRules.Evaluate(
        [
            ("G01", "Project A"),
            ("G01", "Project A"),
            ("G02", "Project A")
        ]);

        var warning = warnings.Should().ContainSingle(item =>
            item.Type == GroupProjectWarningTypes.ProjectHasMultipleGroups).Subject;
        warning.Subject.Should().Be("Project A");
        warning.Related.Should().Equal("G01", "G02");
        warning.Message.Should().Be("Project `Project A` is assigned to multiple groups: `G01`, `G02`.");
    }

    [Fact]
    public void Evaluate_ShouldReportBothDirections_WhenGroupsAndProjectsAreMixedUp()
    {
        var warnings = GroupProjectConsistencyRules.Evaluate(
        [
            ("G01", "Project A"),
            ("G01", "Project B"),
            ("G02", "Project B")
        ]);

        warnings.Select(item => item.Type).Should().BeEquivalentTo(
            GroupProjectWarningTypes.GroupHasMultipleProjects,
            GroupProjectWarningTypes.ProjectHasMultipleGroups);
    }

    [Fact]
    public void Evaluate_ShouldIgnoreStudentsWithoutGroupOrProject()
    {
        var warnings = GroupProjectConsistencyRules.Evaluate(
        [
            ("G01", "Project A"),
            ("G01", null),
            (null, "Project A"),
            ("  ", "Project A"),
            ("G01", "   ")
        ]);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldTreatDifferentSpellingOfTheSameValueAsOne()
    {
        var warnings = GroupProjectConsistencyRules.Evaluate(
        [
            ("g01", "Project A"),
            (" G01 ", " project a "),
            ("G01", "PROJECT A")
        ]);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldSortGroupsNaturally()
    {
        var warnings = GroupProjectConsistencyRules.Evaluate(
        [
            ("G10", "Project A"),
            ("G2", "Project A"),
            ("G1", "Project A")
        ]);

        warnings.Single().Related.Should().Equal("G1", "G2", "G10");
    }
}
