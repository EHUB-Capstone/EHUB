using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.Services;

internal sealed class ClassChatMembershipSynchronizer : IClassChatMembershipSynchronizer
{
    private readonly AppDbContext _context;
    private readonly ILogger<ClassChatMembershipSynchronizer> _logger;

    public ClassChatMembershipSynchronizer(
        AppDbContext context,
        ILogger<ClassChatMembershipSynchronizer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatMembershipSyncResponse> SynchronizeAsync(
        Guid classId,
        Guid? requestedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken)
            ?? throw new InvalidOperationException($"Class '{classId}' no longer exists.");

        var creatorId = await ResolveCreatorIdAsync(targetClass, requestedByUserId, cancellationToken);
        var activeStudentIds = await _context.ClassStudents.AsNoTracking()
            .Where(item => item.ClassId == classId && item.EnrollmentStatus == EnrollmentStatus.Active)
            .Select(item => item.StudentId)
            .ToListAsync(cancellationToken);
        var teams = await _context.Teams.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Status == TeamStatus.Active)
            .Select(item => new { item.Id, item.TeamName })
            .ToListAsync(cancellationToken);
        var teamIds = teams.Select(item => item.Id).ToArray();
        var teamMembers = await _context.TeamMembers.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) && item.CountsTowardActiveTeam)
            .Select(item => new { item.TeamId, item.StudentId })
            .ToListAsync(cancellationToken);
        var mentors = await _context.MentorAssignments.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) &&
                item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
            .Select(item => new { item.TeamId, item.MentorProfile.UserId })
            .ToListAsync(cancellationToken);

        var groups = await _context.ChatGroups
            .Include(item => item.Members)
            .Where(item => item.ClassId == classId &&
                (item.GroupType == ChatGroupType.ClassGroup || item.GroupType == ChatGroupType.TeamGroup))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var groupsCreated = 0;
        var added = 0;
        var reactivated = 0;
        var ended = 0;
        var isReadOnly = targetClass.Status == ClassStatus.Archived;

        var classGroup = groups.FirstOrDefault(item => item.GroupType == ChatGroupType.ClassGroup && item.TeamId == null);
        if (classGroup == null)
        {
            classGroup = CreateGroup(classId, null, $"{targetClass.ClassCode} - Class", ChatGroupType.ClassGroup, creatorId, isReadOnly);
            _context.ChatGroups.Add(classGroup);
            groupsCreated++;
        }
        else
        {
            classGroup.GroupName = $"{targetClass.ClassCode} - Class";
            classGroup.IsReadOnly = isReadOnly;
        }

        var classMembers = activeStudentIds.Select(studentId => DesiredMember.ForStudent(studentId, ChatMemberRole.Student)).ToList();
        if (targetClass.PrimaryLecturerId.HasValue)
            classMembers.Add(DesiredMember.ForUser(targetClass.PrimaryLecturerId.Value, ChatMemberRole.Lecturer));
        SynchronizeMembers(classGroup, classMembers, ref added, ref reactivated, ref ended);

        foreach (var team in teams)
        {
            var group = groups.FirstOrDefault(item => item.GroupType == ChatGroupType.TeamGroup && item.TeamId == team.Id);
            if (group == null)
            {
                group = CreateGroup(classId, team.Id, team.TeamName, ChatGroupType.TeamGroup, creatorId, isReadOnly);
                _context.ChatGroups.Add(group);
                groupsCreated++;
            }
            else
            {
                group.GroupName = team.TeamName;
                group.IsReadOnly = isReadOnly;
            }

            var desired = teamMembers.Where(item => item.TeamId == team.Id)
                .Select(item => DesiredMember.ForStudent(item.StudentId, ChatMemberRole.Student))
                .ToList();
            if (targetClass.PrimaryLecturerId.HasValue)
                desired.Add(DesiredMember.ForUser(targetClass.PrimaryLecturerId.Value, ChatMemberRole.Lecturer));
            desired.AddRange(mentors.Where(item => item.TeamId == team.Id)
                .Select(item => DesiredMember.ForUser(item.UserId, ChatMemberRole.Mentor)));
            SynchronizeMembers(group, desired, ref added, ref reactivated, ref ended);
        }

        foreach (var obsoleteTeamGroup in groups.Where(item =>
                     item.GroupType == ChatGroupType.TeamGroup &&
                     (!item.TeamId.HasValue || !teamIds.Contains(item.TeamId.Value))))
        {
            obsoleteTeamGroup.IsReadOnly = true;
            foreach (var member in obsoleteTeamGroup.Members.Where(item => item.IsActive))
            {
                member.IsActive = false;
                member.LeftAt = DateTime.UtcNow;
                ended++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Synchronized class chat memberships for {ClassId}: {GroupsCreated} groups, {Added} added, {Reactivated} reactivated, {Ended} ended, readOnly={IsReadOnly}",
            classId, groupsCreated, added, reactivated, ended, isReadOnly);

        return new ChatMembershipSyncResponse
        {
            ClassId = classId,
            GroupsCreated = groupsCreated,
            MembershipsAdded = added,
            MembershipsReactivated = reactivated,
            MembershipsEnded = ended,
            IsReadOnly = isReadOnly
        };
    }

    private async Task<Guid> ResolveCreatorIdAsync(Class targetClass, Guid? requestedByUserId, CancellationToken cancellationToken)
    {
        if (requestedByUserId.HasValue && requestedByUserId != Guid.Empty &&
            await _context.Users.AsNoTracking().AnyAsync(item => item.Id == requestedByUserId, cancellationToken))
            return requestedByUserId.Value;
        if (targetClass.CreatedById.HasValue)
            return targetClass.CreatedById.Value;
        if (targetClass.PrimaryLecturerId.HasValue)
            return targetClass.PrimaryLecturerId.Value;

        return await _context.Users.AsNoTracking()
            .Where(item => item.Status == UserStatus.Active)
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);
    }

    private static ChatGroup CreateGroup(
        Guid classId,
        Guid? teamId,
        string name,
        ChatGroupType type,
        Guid creatorId,
        bool isReadOnly) => new()
    {
        ClassId = classId,
        TeamId = teamId,
        GroupName = name,
        GroupType = type,
        CreatedById = creatorId,
        IsReadOnly = isReadOnly
    };

    private void SynchronizeMembers(
        ChatGroup group,
        IReadOnlyCollection<DesiredMember> desired,
        ref int added,
        ref int reactivated,
        ref int ended)
    {
        var desiredKeys = desired.Select(item => item.Key).ToHashSet();
        foreach (var current in group.Members.Where(item => item.IsActive && !desiredKeys.Contains(MemberKey(item))).ToList())
        {
            current.IsActive = false;
            current.LeftAt = DateTime.UtcNow;
            ended++;
        }

        foreach (var desiredMember in desired.DistinctBy(item => item.Key))
        {
            var current = group.Members.FirstOrDefault(item => MemberKey(item) == desiredMember.Key);
            if (current == null)
            {
                group.Members.Add(new ChatGroupMember
                {
                    UserId = desiredMember.UserId,
                    StudentId = desiredMember.StudentId,
                    Role = desiredMember.Role,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                });
                added++;
            }
            else
            {
                current.Role = desiredMember.Role;
                if (!current.IsActive)
                {
                    current.IsActive = true;
                    current.JoinedAt = DateTime.UtcNow;
                    current.LeftAt = null;
                    reactivated++;
                }
            }
        }
    }

    private static string MemberKey(ChatGroupMember member) =>
        member.UserId.HasValue ? $"U:{member.UserId}" : $"S:{member.StudentId}";

    private sealed record DesiredMember(Guid? UserId, Guid? StudentId, ChatMemberRole Role)
    {
        public string Key => UserId.HasValue ? $"U:{UserId}" : $"S:{StudentId}";
        public static DesiredMember ForUser(Guid id, ChatMemberRole role) => new(id, null, role);
        public static DesiredMember ForStudent(Guid id, ChatMemberRole role) => new(null, id, role);
    }
}
