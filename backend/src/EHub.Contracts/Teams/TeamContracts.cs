namespace EHub.Contracts.Teams;

public sealed class TeamMemberDto
{
    public Guid StudentId { get; init; }
    public string RollNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string MajorCode { get; init; } = string.Empty;
    public string RoleInTeam { get; init; } = string.Empty;
    public DateTime JoinedAtUtc { get; init; }
}

public sealed class MentorSummaryDto
{
    public Guid MentorProfileId { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Organization { get; init; }
}

public sealed class MentorAssignmentDto
{
    public Guid AssignmentId { get; init; }
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public MentorSummaryDto Mentor { get; init; } = new();
    public string Status { get; init; } = string.Empty;
    public DateTime AssignedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
    public string? Note { get; init; }
}

public sealed class MentorCandidateDto
{
    public MentorSummaryDto Mentor { get; init; } = new();
    public int ActiveTeamCount { get; init; }
    public int MaxTeams { get; init; }
    public bool HasCapacity { get; init; }
}

public sealed class TeamDto
{
    public Guid Id { get; init; }
    public Guid ClassId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? LeaderId { get; init; }
    public IReadOnlyCollection<TeamMemberDto> Members { get; init; } = Array.Empty<TeamMemberDto>();
    public MentorAssignmentDto? CurrentMentorAssignment { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateTeamRequest
{
    public string TeamName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyCollection<Guid> MemberIds { get; init; } = Array.Empty<Guid>();
    public Guid LeaderStudentId { get; init; }
}

public sealed class UpdateTeamMembersRequest
{
    public string? TeamName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyCollection<Guid> MemberIds { get; init; } = Array.Empty<Guid>();
    public Guid LeaderStudentId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class AssignTeamLeaderRequest
{
    public Guid StudentId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class AssignMentorRequest
{
    public Guid MentorProfileId { get; init; }
    public string? Note { get; init; }
}

public sealed class EndMentorAssignmentRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class TeamProposalMemberDto
{
    public Guid StudentId { get; init; }
    public string RollNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string MajorCode { get; init; } = string.Empty;
    public bool IsLeader { get; init; }
}

public sealed class TeamProposalDto
{
    public Guid Id { get; init; }
    public Guid ClassId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ProjectName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? LatestReviewComment { get; init; }
    public Guid? ApprovedTeamId { get; init; }
    public IReadOnlyCollection<TeamProposalMemberDto> Members { get; init; } = Array.Empty<TeamProposalMemberDto>();
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateTeamProposalRequest
{
    public string TeamName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ProjectName { get; init; }
    public IReadOnlyCollection<Guid> MemberIds { get; init; } = Array.Empty<Guid>();
    public Guid LeaderStudentId { get; init; }
}

public sealed class SubmitTeamProposalRequest
{
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CancelTeamProposalRequest
{
    public string RowVersion { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class UpdateTeamProposalRequest
{
    public string TeamName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ProjectName { get; init; }
    public IReadOnlyCollection<Guid> MemberIds { get; init; } = Array.Empty<Guid>();
    public Guid LeaderStudentId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ReviewTeamProposalRequest
{
    public string Decision { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class TeamProposalHistoryDto
{
    public Guid Id { get; init; }
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public Guid PerformedByUserId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}

public sealed class ProjectDirectionDto
{
    public Guid Id { get; init; }
    public Guid TeamId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? SubmittedAtUtc { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyCollection<ProjectDirectionReviewDto> Reviews { get; init; } = Array.Empty<ProjectDirectionReviewDto>();
}

public sealed class SaveProjectDirectionRequest
{
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? RowVersion { get; init; }
}

public sealed class ProjectDirectionStateRequest
{
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ReviewProjectDirectionRequest
{
    public string Decision { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProjectDirectionReviewDto
{
    public Guid Id { get; init; }
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public Guid ReviewedByUserId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}

public sealed class StudentClassSummaryDto
{
    public Guid Id { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string Semester { get; init; } = string.Empty;
    public int Year { get; init; }
    public string ClassStatus { get; init; } = string.Empty;
    public string EnrollmentStatus { get; init; } = string.Empty;
    public StudentClassLecturerDto? LectureId { get; init; }
    public IReadOnlyCollection<MentorSummaryDto> Mentors { get; init; } = Array.Empty<MentorSummaryDto>();
}

public sealed class StudentClassLecturerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class MyClassesResponse
{
    public IReadOnlyCollection<StudentClassSummaryDto> Classes { get; init; } = Array.Empty<StudentClassSummaryDto>();
}

public sealed class StudentClassDetailResponse
{
    public StudentClassSummaryDto Class { get; init; } = new();
    public IReadOnlyCollection<StudentClassMemberDto> Students { get; init; } = Array.Empty<StudentClassMemberDto>();
    public IReadOnlyCollection<TeamDto> Teams { get; init; } = Array.Empty<TeamDto>();
}

public sealed class StudentClassMemberDto
{
    public Guid StudentId { get; init; }
    public Guid? UserId { get; init; }
    public string RollNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string MajorCode { get; init; } = string.Empty;
    public Guid? TeamId { get; init; }
}

public sealed class MyTeamResponse
{
    public TeamDto? Team { get; init; }
    public StudentClassSummaryDto? Class { get; init; }
    public IReadOnlyCollection<TeamMemberDto> Members { get; init; } = Array.Empty<TeamMemberDto>();
}
