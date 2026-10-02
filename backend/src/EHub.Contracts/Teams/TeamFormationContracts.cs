namespace EHub.Contracts.Teams;

public sealed class CreateTeamFormationRequest
{
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<Guid> MemberStudentIds { get; init; } = Array.Empty<Guid>();
    public Guid LeaderStudentId { get; init; }
}

public sealed class TeamFormationInvitationDto
{
    public Guid StudentId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RollNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsCreator { get; init; }
    public bool IsProposedLeader { get; init; }
}

public sealed class TeamFormationDto
{
    public Guid Id { get; init; }
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid CreatorStudentId { get; init; }
    public Guid MyStudentId { get; init; }
    public Guid ProposedLeaderStudentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? CompletedTeamId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyCollection<TeamFormationInvitationDto> Invitations { get; init; } = Array.Empty<TeamFormationInvitationDto>();
}
