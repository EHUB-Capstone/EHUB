using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Teams.Common;

internal static class TeamMappings
{
    public static TeamDto ToDto(Team team)
    {
        var activeAssignment = team.MentorAssignments
            .Where(assignment => assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null)
            .OrderByDescending(assignment => assignment.AssignedAt)
            .FirstOrDefault();
        var members = team.TeamMembers
            .Where(member => member.CountsTowardActiveTeam)
            .OrderBy(member => member.RoleInTeam == TeamMemberRole.Leader ? 0 : 1)
            .ThenBy(member => member.ClassStudent.Student.RollNumber)
            .Select(ToMemberDto)
            .ToArray();

        return new TeamDto
        {
            Id = team.Id,
            ClassId = team.ClassId,
            TeamCode = team.TeamCode,
            TeamName = team.TeamName,
            Description = team.Description,
            Status = team.Status.ToString(),
            LeaderId = members.FirstOrDefault(member => member.RoleInTeam == TeamMemberRole.Leader.ToString())?.StudentId,
            Members = members,
            CurrentMentorAssignment = activeAssignment == null ? null : ToMentorAssignmentDto(activeAssignment),
            RowVersion = team.Version.ToString()
        };
    }

    public static TeamMemberDto ToMemberDto(TeamMember member) => new()
    {
        StudentId = member.StudentId,
        RollNumber = member.ClassStudent.Student.RollNumber ?? string.Empty,
        FullName = member.ClassStudent.Student.FullName,
        Email = member.ClassStudent.Student.Email,
        MajorCode = member.ClassStudent.MajorCodeAtEnrollment,
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
            Organization = assignment.MentorProfile.Organization
        },
        Status = assignment.Status.ToString(),
        AssignedAtUtc = assignment.AssignedAt,
        EndedAtUtc = assignment.EndedAt,
        Note = assignment.Note
    };
}
