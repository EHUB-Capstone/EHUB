using EHub.Contracts.Showcase;

namespace EHub.Application.Features.Showcase;

internal sealed record LatestProjectMember(Guid? UserId, string FullName, string? AvatarUrl, string Role);

internal sealed record LatestProject(
    Guid ProjectId,
    Guid TeamId,
    string ProjectName,
    string Description,
    string? Problem,
    string? Solution,
    string? TargetUsers,
    string TeamName,
    string ClassCode,
    IReadOnlyCollection<Guid> LecturerUserIds,
    IReadOnlyCollection<LatestProjectMember> Members)
{
    public IReadOnlyCollection<Guid> MemberUserIds =>
        Members.Where(member => member.UserId.HasValue).Select(member => member.UserId!.Value).Distinct().ToArray();

    /// <summary>The Project Profile fields shown on the showcase, read live so the two pages cannot disagree.</summary>
    public ShowcaseProfileDto Profile => ShowcaseMapper.ToProfile(ProjectName, Description, Problem, Solution, TargetUsers);
}
