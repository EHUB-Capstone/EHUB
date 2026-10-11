namespace EHub.Contracts.Teams;

public sealed class CreateTeamFormationRequest
{
    public string TeamName { get; init; } = string.Empty;
    /// <summary>Students to invite. Must not contain the creator, who is added automatically.</summary>
    public IReadOnlyCollection<Guid> InviteeStudentIds { get; init; } = Array.Empty<Guid>();
    /// <summary>Proposed Team Leader: the creator or one of the invitees.</summary>
    public Guid LeaderStudentId { get; init; }
}

public sealed class InviteTeamFormationMembersRequest
{
    public IReadOnlyCollection<Guid> StudentIds { get; init; } = Array.Empty<Guid>();
}

public sealed class FinalizeTeamFormationRequest
{
    /// <summary>
    /// Required when the proposed leader has not accepted. Must be empty or equal to the proposed leader otherwise.
    /// </summary>
    public Guid? LeaderStudentId { get; init; }
    /// <summary>Must be true when invitations are still pending, to confirm they will be closed.</summary>
    public bool ConfirmPendingInvitations { get; init; }
}

public sealed class TeamFormationInvitationDto
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RollNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsCreator { get; init; }
    public bool IsProposedLeader { get; init; }
    /// <summary>True for the most recent record of this student in the formation.</summary>
    public bool IsCurrent { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? RespondedAtUtc { get; init; }
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
    public DateTime ServerTimeUtc { get; init; }
    public int AcceptedCount { get; init; }
    public int PendingCount { get; init; }
    public int ActiveCount { get; init; }
    public bool CanFinalize { get; init; }
    public IReadOnlyCollection<string> FinalizeBlockers { get; init; } = Array.Empty<string>();
    /// <summary>True when the proposed leader is not an accepted member, so the creator must pick one at finalize.</summary>
    public bool RequiresLeaderSelection { get; init; }
    /// <summary>History included: several records per student, oldest first.</summary>
    public IReadOnlyCollection<TeamFormationInvitationDto> Invitations { get; init; } = Array.Empty<TeamFormationInvitationDto>();
}
