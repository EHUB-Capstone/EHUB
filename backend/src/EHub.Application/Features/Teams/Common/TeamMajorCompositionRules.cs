using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Teams.Common;

/// <summary>
/// Business rule for a team's major structure: at least one member from GROUP_1 (BBA/BEN)
/// and one member from GROUP_2 (BIT). Ratio between the groups does not matter.
/// </summary>
public static class TeamMajorCompositionRules
{
    public const string GroupOne = "GROUP_1";
    public const string GroupTwo = "GROUP_2";

    private static readonly HashSet<string> GroupOneMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        MajorCodes.BBA_HM,
        MajorCodes.BBA_FIN,
        MajorCodes.BBA_IB,
        MajorCodes.BBA_MC,
        MajorCodes.BBA_MKT,
        MajorCodes.BEN,
        MajorCodes.BBA_TM
    };

    private static readonly HashSet<string> GroupTwoMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        MajorCodes.BIT_AI,
        MajorCodes.BIT_GD,
        MajorCodes.BIT_IA,
        MajorCodes.BIT_SE
    };

    public static bool IsGroupOne(string? majorCode) =>
        !string.IsNullOrWhiteSpace(majorCode) && GroupOneMajorCodes.Contains(majorCode.Trim());

    public static bool IsGroupTwo(string? majorCode) =>
        !string.IsNullOrWhiteSpace(majorCode) && GroupTwoMajorCodes.Contains(majorCode.Trim());

    public static TeamMajorCompositionDto Evaluate(IEnumerable<(string Name, string? MajorCode)> members)
    {
        var memberList = members.ToArray();
        var missingGroups = new List<string>();
        if (!memberList.Any(member => IsGroupOne(member.MajorCode))) missingGroups.Add(GroupOne);
        if (!memberList.Any(member => IsGroupTwo(member.MajorCode))) missingGroups.Add(GroupTwo);

        if (missingGroups.Count == 0)
        {
            return new TeamMajorCompositionDto { IsValid = true };
        }

        var membersWithoutValidMajor = memberList
            .Where(member => !IsGroupOne(member.MajorCode) && !IsGroupTwo(member.MajorCode))
            .Select(member => member.Name)
            .ToArray();

        var missingText = string.Join(" and ", missingGroups.Select(group =>
            group == GroupOne ? "GROUP_1 (BBA/BEN)" : "GROUP_2 (BIT)"));
        var message = $"This team does not meet the major requirement: it has no member from {missingText}. " +
                      "A team needs at least one GROUP_1 and one GROUP_2 major.";
        if (membersWithoutValidMajor.Length > 0)
        {
            message += $" {membersWithoutValidMajor.Length} member(s) have no valid major: " +
                       $"{string.Join(", ", membersWithoutValidMajor)}.";
        }

        return new TeamMajorCompositionDto
        {
            IsValid = false,
            MissingGroups = missingGroups,
            MembersWithoutValidMajor = membersWithoutValidMajor,
            Message = message
        };
    }

    public static TeamMajorCompositionDto Evaluate(Team team) => Evaluate(team.TeamMembers
        .Where(member => member.CountsTowardActiveTeam)
        .Select(member => (
            member.ClassStudent.Student.FullName,
            StudentEnrollmentRules.ResolveEffectiveMajorCode(
                member.ClassStudent.MajorCodeAtEnrollment,
                member.ClassStudent.Student.MajorCode))));
}
