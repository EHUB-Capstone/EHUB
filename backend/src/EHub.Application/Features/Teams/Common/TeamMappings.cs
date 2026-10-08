using EHub.Contracts.Teams;
using EHub.Application.Features.Classes.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Teams.Common;

internal static class TeamMappings
{
    public static TeamDto ToDto(Team team) => ToDto(team, true);

    public static TeamDto ToDto(Team team, bool includePrivateInformation)
    {
        // Completing a class ends its assignments at the completion time. Those still describe who mentored the team,
        // so they are shown for a completed class; a mentor replaced earlier in the term is not.
        var completedAt = team.Class?.CompletedAtUtc;
        var activeAssignments = team.MentorAssignments
            .Where(assignment => (assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null) ||
                (completedAt != null && assignment.Status == MentorAssignmentStatus.Ended &&
                 assignment.EndedAt != null && assignment.EndedAt >= completedAt))
            .OrderBy(assignment => assignment.Slot)
            .ThenByDescending(assignment => assignment.AssignedAt)
            .Select(ToMentorAssignmentDto)
            .ToArray();
        var members = team.TeamMembers
            .Where(member => member.CountsTowardActiveTeam)
            .OrderBy(member => member.RoleInTeam == TeamMemberRole.Leader ? 0 : 1)
            .ThenBy(member => member.ClassStudent.Student.RollNumber)
            .Select(member => ToMemberDto(member, includePrivateInformation))
            .ToArray();

        return new TeamDto
        {
            Id = team.Id,
            ClassId = team.ClassId,
            TeamCode = team.TeamCode,
            TeamName = team.TeamName,
            Description = team.Description,
            ProjectName = team.Project?.Name,
            ProjectDescription = team.Project?.Description,
            Status = team.Status == TeamStatus.Active ? "APPROVED" : team.Status.ToString(),
            HasChatGroup = team.ChatGroups.Any(group => !group.IsReadOnly),
            LeaderId = members.FirstOrDefault(member => member.RoleInTeam == TeamMemberRole.Leader.ToString())?.StudentId,
            Members = members,
            CurrentMentorAssignments = activeAssignments,
            CurrentMentorAssignment = activeAssignments.FirstOrDefault(),
            MajorComposition = TeamMajorCompositionRules.Evaluate(team),
            TeamLineageId = team.TeamLineageId,
            IsContinued = team.PreviousTeamId.HasValue,
            ContinuedFromSemesterCode = team.PreviousTeam?.Class?.Semester?.Code,
            ContinuedFromClassCode = team.PreviousTeam?.Class?.ClassCode,
            RowVersion = team.Version.ToString()
        };
    }

    public static TeamMemberDto ToMemberDto(TeamMember member) => ToMemberDto(member, true);

    public static TeamMemberDto ToMemberDto(TeamMember member, bool includePrivateInformation) => new()
    {
        StudentId = member.StudentId,
        RollNumber = member.ClassStudent.Student.RollNumber ?? string.Empty,
        FullName = member.ClassStudent.Student.FullName,
        Email = includePrivateInformation ? member.ClassStudent.Student.Email : null,
        MajorCode = StudentEnrollmentRules.ResolveEffectiveMajorCode(
            member.ClassStudent.MajorCodeAtEnrollment,
            member.ClassStudent.Student.MajorCode) ?? string.Empty,
        RoleInTeam = member.RoleInTeam.ToString(),
        JoinedAtUtc = member.JoinedAt
    };

    public static MentorAssignmentDto ToMentorAssignmentDto(MentorAssignment assignment) => new()
    {
        AssignmentId = assignment.Id,
        TeamId = assignment.TeamId,
        TeamName = assignment.Team.TeamName,
        ClassId = assignment.Team.ClassId,
        Mentor = new MentorSummaryDto
        {
            MentorProfileId = assignment.MentorProfileId,
            UserId = assignment.MentorProfile.UserId,
            FullName = assignment.MentorProfile.User.FullName,
            Email = assignment.MentorProfile.User.Email,
            Organization = assignment.MentorProfile.Organization,
            MentorType = assignment.MentorProfile.Type.ToString(),
            Department = assignment.MentorProfile.Department,
            JobTitle = assignment.MentorProfile.JobTitle,
            ContractType = assignment.MentorProfile.ContractType
        },
        Status = assignment.Status.ToString(),
        AssignedAtUtc = assignment.AssignedAt,
        EndedAtUtc = assignment.EndedAt,
        Note = assignment.Note,
        Slot = assignment.Slot.ToString()
    };
}
