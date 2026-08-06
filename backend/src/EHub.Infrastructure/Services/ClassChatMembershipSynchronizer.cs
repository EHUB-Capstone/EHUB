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

        // Auto-link missing UserId for students if a matching User account exists by email
        var activeClassStudents = await _context.ClassStudents.AsNoTracking()
            .Where(cs => cs.ClassId == classId && cs.EnrollmentStatus == EnrollmentStatus.Active)
            .Select(cs => new { cs.StudentId, cs.Student.UserId, cs.Student.Email })
            .Distinct()
            .ToListAsync(cancellationToken);

        var unlinkedStudentIds = activeClassStudents
            .Where(s => !s.UserId.HasValue && !string.IsNullOrWhiteSpace(s.Email))
            .Select(s => s.StudentId)
            .ToList();

        if (unlinkedStudentIds.Count > 0)
        {
            var unlinkedStudents = await _context.Students
                .Where(s => unlinkedStudentIds.Contains(s.Id))
                .ToListAsync(cancellationToken);

            foreach (var student in unlinkedStudents)
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    var normalizedEmail = student.Email.Trim().ToLower();
                    var matchingUser = await _context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail, cancellationToken);
                    if (matchingUser != null)
                    {
                        var isAlreadyLinked = await _context.Students.AsNoTracking()
                            .AnyAsync(s => s.UserId == matchingUser.Id && s.Id != student.Id, cancellationToken);
                        if (!isAlreadyLinked)
                        {
                            student.UserId = matchingUser.Id;
                            student.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
            }
        }

        var activeStudentMap = await _context.ClassStudents.AsNoTracking()
            .Where(cs => cs.ClassId == classId && cs.EnrollmentStatus == EnrollmentStatus.Active)
            .Select(cs => new StudentIdentity(cs.StudentId, cs.Student != null ? cs.Student.UserId : (Guid?)null))
            .Distinct()
            .ToListAsync(cancellationToken);

        var teams = await _context.Teams.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Status == TeamStatus.Active)
            .Select(item => new { item.Id, item.TeamName })
            .ToListAsync(cancellationToken);
        var teamIds = teams.Select(item => item.Id).ToArray();

        var teamMembers = await _context.TeamMembers.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) && item.CountsTowardActiveTeam)
            .Select(item => new { item.TeamId, item.StudentId, UserId = item.ClassStudent != null && item.ClassStudent.Student != null ? item.ClassStudent.Student.UserId : (Guid?)null })
            .ToListAsync(cancellationToken);

        var mentors = await _context.MentorAssignments.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) &&
                item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
            .Select(item => new { item.TeamId, UserId = item.MentorProfile != null ? item.MentorProfile.UserId : Guid.Empty })
            .Where(item => item.UserId != Guid.Empty)
            .ToListAsync(cancellationToken);

        var groups = await _context.ChatGroups
            .IgnoreQueryFilters()
            .Where(item => item.ClassId == classId && !item.IsDeleted &&
                (item.GroupType == ChatGroupType.ClassGroup || item.GroupType == ChatGroupType.TeamGroup))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(g => g.Id).ToList();
        var allMembers = await _context.ChatGroupMembers
            .IgnoreQueryFilters()
            .Where(m => groupIds.Contains(m.ChatGroupId))
            .ToListAsync(cancellationToken);

        foreach (var group in groups)
        {
            var membersForGroup = allMembers.Where(m => m.ChatGroupId == group.Id).ToList();
            foreach (var member in membersForGroup)
            {
                if (!group.Members.Contains(member))
                {
                    group.Members.Add(member);
                }
            }
        }

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
            if (classGroup.GroupName != $"{targetClass.ClassCode} - Class" || classGroup.IsReadOnly != isReadOnly)
            {
                classGroup.GroupName = $"{targetClass.ClassCode} - Class";
                classGroup.IsReadOnly = isReadOnly;
            }
        }

        var desiredClassMembers = activeStudentMap
            .DistinctBy(s => s.StudentId)
            .Select(s => DesiredMember.ForStudent(s.StudentId, s.UserId, ChatMemberRole.Student))
            .ToList();
        if (targetClass.PrimaryLecturerId.HasValue)
            desiredClassMembers.Add(DesiredMember.ForUser(targetClass.PrimaryLecturerId.Value, ChatMemberRole.Lecturer));

        SynchronizeMembers(classGroup, desiredClassMembers, ref added, ref reactivated, ref ended);

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
                if (group.GroupName != team.TeamName || group.IsReadOnly != isReadOnly)
                {
                    group.GroupName = team.TeamName;
                    group.IsReadOnly = isReadOnly;
                }
            }

            var desiredTeamMembers = teamMembers.Where(item => item.TeamId == team.Id)
                .DistinctBy(item => item.StudentId)
                .Select(item => DesiredMember.ForStudent(item.StudentId, item.UserId, ChatMemberRole.Student))
                .ToList();
            if (targetClass.PrimaryLecturerId.HasValue)
                desiredTeamMembers.Add(DesiredMember.ForUser(targetClass.PrimaryLecturerId.Value, ChatMemberRole.Lecturer));
            desiredTeamMembers.AddRange(mentors.Where(item => item.TeamId == team.Id)
                .Select(item => DesiredMember.ForUser(item.UserId, ChatMemberRole.Mentor)));

            SynchronizeMembers(group, desiredTeamMembers, ref added, ref reactivated, ref ended);
        }

        foreach (var obsoleteTeamGroup in groups.Where(item =>
                     item.GroupType == ChatGroupType.TeamGroup &&
                     (!item.TeamId.HasValue || !teamIds.Contains(item.TeamId.Value))))
        {
            if (!obsoleteTeamGroup.IsReadOnly)
                obsoleteTeamGroup.IsReadOnly = true;

            foreach (var member in obsoleteTeamGroup.Members.Where(item => item.IsActive && !item.IsDeleted))
            {
                member.IsActive = false;
                member.LeftAt = DateTime.UtcNow;
                ended++;
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "DbUpdateConcurrencyException during chat synchronization for class {ClassId}. Resolving entry values and retrying.", classId);
            foreach (var entry in ex.Entries)
            {
                if (entry.State == EntityState.Added)
                {
                    continue;
                }

                var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                if (databaseValues == null)
                {
                    entry.State = EntityState.Detached;
                }
                else
                {
                    entry.OriginalValues.SetValues(databaseValues);
                }
            }
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Retry SaveChanges after concurrency resolution failed for class {ClassId}", classId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving synchronized chat memberships for class {ClassId}", classId);
            throw;
        }

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

        var firstUser = await _context.Users.AsNoTracking()
            .Where(item => item.Status == UserStatus.Active)
            .OrderBy(item => item.CreatedAt)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return firstUser ?? requestedByUserId ?? Guid.NewGuid();
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
        // 1. Process desired members: keep one primary member per desired user/student, cleanup duplicates
        foreach (var desiredMember in desired)
        {
            var matchingMembers = group.Members
                .Where(item => IsMatch(item, desiredMember))
                .OrderByDescending(item => item.StudentId.HasValue)
                .ThenByDescending(item => item.IsActive && !item.IsDeleted)
                .ToList();

            if (matchingMembers.Count == 0)
            {
                var newMember = new ChatGroupMember
                {
                    ChatGroupId = group.Id,
                    UserId = desiredMember.UserId,
                    StudentId = desiredMember.StudentId,
                    Role = desiredMember.Role,
                    IsActive = true,
                    IsDeleted = false,
                    JoinedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                group.Members.Add(newMember);
                _context.ChatGroupMembers.Add(newMember);
                added++;
            }
            else
            {
                var primary = matchingMembers.First();
                primary.Role = desiredMember.Role;

                if (primary.IsDeleted)
                {
                    primary.IsDeleted = false;
                    primary.DeletedAt = null;
                    primary.DeletedBy = null;
                }

                if (!primary.IsActive)
                {
                    primary.IsActive = true;
                    primary.JoinedAt = DateTime.UtcNow;
                    primary.LeftAt = null;
                    reactivated++;
                }

                // Cleanup all other duplicate legacy members for this user/student in the group
                foreach (var duplicate in matchingMembers.Skip(1))
                {
                    if (duplicate.IsActive || !duplicate.IsDeleted)
                    {
                        duplicate.IsActive = false;
                        duplicate.IsDeleted = true;
                        duplicate.DeletedAt = DateTime.UtcNow;
                        duplicate.LeftAt = DateTime.UtcNow;
                        ended++;
                    }
                }
            }
        }

        // 2. Deactivate any member that is no longer desired and not already cleaned up
        foreach (var current in group.Members.Where(item => item.IsActive && !item.IsDeleted).ToList())
        {
            var isStillDesired = desired.Any(d => IsMatch(current, d));
            if (!isStillDesired)
            {
                current.IsActive = false;
                current.LeftAt = DateTime.UtcNow;
                ended++;
            }
        }
    }

    private static bool IsMatch(ChatGroupMember member, DesiredMember desired)
    {
        if (desired.UserId.HasValue && member.UserId.HasValue && member.UserId == desired.UserId.Value)
            return true;

        if (desired.StudentId.HasValue && member.StudentId.HasValue && member.StudentId == desired.StudentId.Value)
            return true;

        if (desired.AssociatedUserId.HasValue && member.UserId.HasValue && member.UserId == desired.AssociatedUserId.Value)
            return true;

        return false;
    }

    private sealed record StudentIdentity(Guid StudentId, Guid? UserId);

    private sealed record DesiredMember(Guid? UserId, Guid? StudentId, Guid? AssociatedUserId, ChatMemberRole Role)
    {
        public static DesiredMember ForUser(Guid id, ChatMemberRole role) => new(id, null, null, role);
        public static DesiredMember ForStudent(Guid studentId, Guid? userId, ChatMemberRole role)
        {
            return new(null, studentId, userId, role);
        }
    }
}
