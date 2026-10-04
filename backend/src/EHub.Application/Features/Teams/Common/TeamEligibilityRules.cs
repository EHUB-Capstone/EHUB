namespace EHub.Application.Features.Teams.Common;

public sealed record TeamEligibilityResult(bool IsEligible, int MemberCount, IReadOnlyList<string> Reasons);

/// <summary>
/// Single place for the "can this set of members form a team" rule: 4 to 6 members with
/// at least one GROUP_1 and one GROUP_2 major.
/// </summary>
public static class TeamEligibilityRules
{
    public const int MinimumMembers = 4;
    public const int MaximumMembers = 6;

    public static TeamEligibilityResult Evaluate(IReadOnlyCollection<string?> memberMajorCodes)
    {
        var reasons = new List<string>();
        if (memberMajorCodes.Count < MinimumMembers)
            reasons.Add($"Only {memberMajorCodes.Count} member(s) are available; a team needs at least {MinimumMembers}.");
        if (memberMajorCodes.Count > MaximumMembers)
            reasons.Add($"{memberMajorCodes.Count} members are available; a team can have at most {MaximumMembers}.");
        if (!memberMajorCodes.Any(TeamMajorCompositionRules.IsGroupOne))
            reasons.Add("No member from GROUP_1 (BBA/BEN).");
        if (!memberMajorCodes.Any(TeamMajorCompositionRules.IsGroupTwo))
            reasons.Add("No member from GROUP_2 (BIT).");

        return new TeamEligibilityResult(reasons.Count == 0, memberMajorCodes.Count, reasons);
    }
}
