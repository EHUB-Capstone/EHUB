namespace EHub.Application.Features.Teams.TeamFormations;

public static class TeamFormationRules
{
    public const int MinMembers = 4;
    public const int MaxMembers = 6;
    public const int MinTeamNameLength = 3;
    public const int MaxTeamNameLength = 60;
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(24);

    public static readonly HashSet<string> GroupOneMajors = new(StringComparer.OrdinalIgnoreCase)
    {
        "BBA_HM", "BBA_IB", "BBA_MC", "BBA_MKT", "BEN", "BBA_TM", "BBA_FIN"
    };

    public static readonly HashSet<string> GroupTwoMajors = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIT_AI", "BIT_GD", "BIT_IA", "BIT_SE"
    };

    public static bool HasGroupOne(IEnumerable<string?> majors) =>
        majors.Any(major => major is not null && GroupOneMajors.Contains(major));

    public static bool HasGroupTwo(IEnumerable<string?> majors) =>
        majors.Any(major => major is not null && GroupTwoMajors.Contains(major));
}
