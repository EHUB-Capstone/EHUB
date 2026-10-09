using EHub.Application.Features.Teams.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Teams;

public class TeamEligibilityRulesTests
{
    private static readonly string[] Group1 = [MajorCodes.BBA_MKT, MajorCodes.BBA_FIN, MajorCodes.BEN];
    private static readonly string[] Group2 = [MajorCodes.BIT_SE, MajorCodes.BIT_AI, MajorCodes.BIT_GD];

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 2)]
    public void Evaluate_AcceptsFourToSixMembersWithBothGroups(int groupOne, int groupTwo)
    {
        var majors = Group1.Take(groupOne).Concat(Group2.Take(groupTwo)).Cast<string?>().ToArray();

        var result = TeamEligibilityRules.Evaluate(majors);

        result.IsEligible.Should().Be(majors.Length is >= 4 and <= 6);
    }

    [Fact]
    public void Evaluate_RejectsFewerThanFourMembers()
    {
        var result = TeamEligibilityRules.Evaluate([MajorCodes.BBA_MKT, MajorCodes.BIT_SE, MajorCodes.BIT_AI]);

        result.IsEligible.Should().BeFalse();
        result.MemberCount.Should().Be(3);
        result.Reasons.Should().ContainSingle().Which.Should().Contain("at least 4");
    }

    [Fact]
    public void Evaluate_RejectsMoreThanSixMembers()
    {
        string?[] majors =
        [
            MajorCodes.BBA_MKT, MajorCodes.BBA_FIN, MajorCodes.BEN,
            MajorCodes.BIT_SE, MajorCodes.BIT_AI, MajorCodes.BIT_GD, MajorCodes.BIT_IA
        ];

        var result = TeamEligibilityRules.Evaluate(majors);

        result.IsEligible.Should().BeFalse();
        result.Reasons.Should().ContainSingle().Which.Should().Contain("at most 6");
    }

    [Theory]
    [InlineData(MajorCodes.BIT_SE, "GROUP_1")]
    [InlineData(MajorCodes.BBA_MKT, "GROUP_2")]
    public void Evaluate_ReportsTheMissingMajorGroup(string onlyMajor, string missingGroup)
    {
        var result = TeamEligibilityRules.Evaluate([onlyMajor, onlyMajor, onlyMajor, onlyMajor]);

        result.IsEligible.Should().BeFalse();
        result.Reasons.Should().ContainSingle().Which.Should().Contain(missingGroup);
    }

    [Fact]
    public void Evaluate_DoesNotCountMembersWithoutAValidMajor()
    {
        var result = TeamEligibilityRules.Evaluate([MajorCodes.BBA_MKT, MajorCodes.BIT_SE, null, ""]);

        // Four members, but only two with a valid major; both groups are present so the team is eligible.
        result.IsEligible.Should().BeTrue();
        TeamEligibilityRules.Evaluate([null, "", "UNKNOWN", MajorCodes.BIT_SE]).IsEligible.Should().BeFalse();
    }
}
