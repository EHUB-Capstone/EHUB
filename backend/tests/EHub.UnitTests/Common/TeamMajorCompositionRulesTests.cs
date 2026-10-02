using EHub.Application.Features.Teams.Common;
using EHub.Domain.Entities;
using EHub.Shared.Constants;
using FluentAssertions;

namespace EHub.UnitTests.Common;

public sealed class TeamMajorCompositionRulesTests
{
    [Theory]
    [InlineData(MajorCodes.BBA_MKT, MajorCodes.BIT_SE)]
    [InlineData(MajorCodes.BEN, MajorCodes.BIT_AI)]
    [InlineData(" bba_fin ", " bit_gd ")]
    public void Evaluate_ShouldBeValid_WhenTeamHasGroupOneAndGroupTwoMembers(string groupOneMajor, string groupTwoMajor)
    {
        var result = TeamMajorCompositionRules.Evaluate(
        [
            ("A", groupOneMajor),
            ("B", groupTwoMajor),
            ("C", groupTwoMajor),
            ("D", groupOneMajor)
        ]);

        result.IsValid.Should().BeTrue();
        result.Message.Should().BeNull();
        result.MissingGroups.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldWarnAboutGroupTwo_WhenEveryMemberIsGroupOne()
    {
        var result = TeamMajorCompositionRules.Evaluate(
        [
            ("A", MajorCodes.BBA_MKT),
            ("B", MajorCodes.BBA_FIN),
            ("C", MajorCodes.BEN),
            ("D", MajorCodes.BBA_HM)
        ]);

        result.IsValid.Should().BeFalse();
        result.MissingGroups.Should().Equal(TeamMajorCompositionRules.GroupTwo);
        result.Message.Should().Contain("GROUP_2");
        result.Message.Should().NotContain("no member from GROUP_1");
    }

    [Fact]
    public void Evaluate_ShouldWarnAboutGroupOne_WhenEveryMemberIsGroupTwo()
    {
        var result = TeamMajorCompositionRules.Evaluate(
        [
            ("A", MajorCodes.BIT_AI),
            ("B", MajorCodes.BIT_SE),
            ("C", MajorCodes.BIT_GD),
            ("D", MajorCodes.BIT_IA)
        ]);

        result.IsValid.Should().BeFalse();
        result.MissingGroups.Should().Equal(TeamMajorCompositionRules.GroupOne);
    }

    [Fact]
    public void Evaluate_ShouldListMembersWithoutValidMajor_WhenMajorsAreMissingOrUnsupported()
    {
        var result = TeamMajorCompositionRules.Evaluate(
        [
            ("Alice", MajorCodes.BIT_SE),
            ("Bob", MajorCodes.Undeclared),
            ("Carol", null),
            ("Dave", "BIT_IS")
        ]);

        result.IsValid.Should().BeFalse();
        result.MissingGroups.Should().Equal(TeamMajorCompositionRules.GroupOne);
        result.MembersWithoutValidMajor.Should().BeEquivalentTo("Bob", "Carol", "Dave");
        result.Message.Should().Contain("Bob").And.Contain("Carol").And.Contain("Dave");
    }

    [Fact]
    public void Evaluate_ShouldRequireBothGroups_WhenTeamHasNoMembers()
    {
        var result = TeamMajorCompositionRules.Evaluate([]);

        result.IsValid.Should().BeFalse();
        result.MissingGroups.Should().Equal(TeamMajorCompositionRules.GroupOne, TeamMajorCompositionRules.GroupTwo);
    }

    [Fact]
    public void EvaluateTeam_ShouldUseVerifiedEnrollmentMajor_AndIgnoreInactiveMembers()
    {
        var team = new Team
        {
            TeamMembers =
            [
                CreateMember("Leader", MajorCodes.BBA_MKT, profileMajor: MajorCodes.BIT_SE),
                CreateMember("Member", MajorCodes.BBA_FIN, profileMajor: MajorCodes.BIT_AI),
                // Former member: their group-two major must not satisfy the requirement.
                CreateMember("Former", MajorCodes.BIT_SE, profileMajor: MajorCodes.BIT_SE, countsTowardActiveTeam: false)
            ]
        };

        var result = TeamMajorCompositionRules.Evaluate(team);

        result.IsValid.Should().BeFalse();
        result.MissingGroups.Should().Equal(TeamMajorCompositionRules.GroupTwo);
    }

    [Fact]
    public void EvaluateTeam_ShouldFallBackToProfileMajor_WhenEnrollmentMajorIsUndeclared()
    {
        var team = new Team
        {
            TeamMembers =
            [
                CreateMember("Leader", MajorCodes.BBA_MKT, profileMajor: null),
                CreateMember("Member", MajorCodes.Undeclared, profileMajor: MajorCodes.BIT_SE)
            ]
        };

        TeamMajorCompositionRules.Evaluate(team).IsValid.Should().BeTrue();
    }

    private static TeamMember CreateMember(
        string name,
        string enrollmentMajor,
        string? profileMajor,
        bool countsTowardActiveTeam = true) => new()
    {
        CountsTowardActiveTeam = countsTowardActiveTeam,
        ClassStudent = new ClassStudent
        {
            MajorCodeAtEnrollment = enrollmentMajor,
            Student = new Student { FullName = name, MajorCode = profileMajor }
        }
    };
}
