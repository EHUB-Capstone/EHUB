using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.Teams.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.Continuations;

public sealed class TeamContinuationService : ITeamContinuationService
{
    private const string CodeSuffixPrefix = "_TEAM_";
    private const int MaximumTeamCodeLength = 50;
    private const int MaximumTeamNameLength = 100;

    private readonly IApplicationDbContext _context;

    public TeamContinuationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<TeamContinuationSummaryDto> PreviewAsync(
        Class targetClass,
        IReadOnlyCollection<PendingContinuationStudent> pendingStudents,
        CancellationToken cancellationToken = default) =>
        RunAsync(targetClass, pendingStudents, Guid.Empty, apply: false, cancellationToken);

    public Task<TeamContinuationSummaryDto> ApplyAsync(
        Class targetClass,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        RunAsync(targetClass, [], actorUserId, apply: true, cancellationToken);

    private sealed record RosterStudent(Guid StudentId, string? MajorCode, ClassStudent? Enrollment, string SortKey);

    private async Task<TeamContinuationSummaryDto> RunAsync(
        Class targetClass,
        IReadOnlyCollection<PendingContinuationStudent> pendingStudents,
        Guid actorUserId,
        bool apply,
        CancellationToken ct)
    {
        var previousSemester = await FindPreviousSemesterAsync(targetClass.SemesterId, ct);
        if (previousSemester == null) return new TeamContinuationSummaryDto();

        var roster = await LoadRosterAsync(targetClass.Id, pendingStudents, apply, ct);
        if (roster.Count == 0) return new TeamContinuationSummaryDto { SourceSemesterCode = previousSemester.Code };
        var rosterIds = roster.Keys.ToArray();

        var blocked = await LoadBlockedStudentIdsAsync(targetClass.Id, rosterIds, ct);

        var sourceTeams = await _context.Teams
            .AsNoTracking()
            .Include(team => team.Class)
            .Include(team => team.TeamMembers)
            .Include(team => team.Project).ThenInclude(project => project!.ProjectTags)
            .Where(team =>
                team.Class.SemesterId == previousSemester.Id &&
                team.Status != TeamStatus.Disabled &&
                team.TeamMembers.Any(member => member.CountsTowardActiveTeam && rosterIds.Contains(member.StudentId)))
            .OrderBy(team => team.TeamCode)
            .ToListAsync(ct);
        if (sourceTeams.Count == 0) return new TeamContinuationSummaryDto { SourceSemesterCode = previousSemester.Code };

        var lineageIds = sourceTeams.Select(team => team.TeamLineageId).Distinct().ToArray();
        var continuations = (await _context.TeamContinuations
                .Include(c => c.Members)
                .Where(c => lineageIds.Contains(c.TeamLineageId) && c.TargetSemesterId == targetClass.SemesterId)
                .ToListAsync(ct))
            .ToDictionary(c => c.TeamLineageId);

        var createdTeamIds = continuations.Values
            .Where(c => c.CreatedTeamId.HasValue)
            .Select(c => c.CreatedTeamId!.Value)
            .ToArray();
        var continuedTeams = createdTeamIds.Length == 0
            ? new Dictionary<Guid, Team>()
            : await _context.Teams
                .Include(team => team.TeamMembers)
                .Where(team => createdTeamIds.Contains(team.Id))
                .ToDictionaryAsync(team => team.Id, ct);

        var teamNames = (await _context.Teams.IgnoreQueryFilters().AsNoTracking()
                .Where(team => team.ClassId == targetClass.Id)
                .Select(team => team.TeamName)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var proposalNames = (await _context.TeamProposals.AsNoTracking()
                .Where(proposal =>
                    proposal.ClassId == targetClass.Id &&
                    proposal.Status != TeamProposalStatus.Rejected &&
                    proposal.Status != TeamProposalStatus.Cancelled)
                .Select(proposal => proposal.TeamName)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codePrefix = CreateTeamCodePrefix(targetClass.ClassCode);
        var nextSequence = GetHighestTeamSequence(
            await _context.Teams.IgnoreQueryFilters().AsNoTracking()
                .Where(team => team.ClassId == targetClass.Id && team.TeamCode.StartsWith(codePrefix))
                .Select(team => team.TeamCode)
                .ToListAsync(ct),
            codePrefix);

        var now = DateTime.UtcNow;
        var items = new List<TeamContinuationItemDto>();
        var changed = false;

        foreach (var source in sourceTeams)
        {
            var candidates = source.TeamMembers
                .Where(member => member.CountsTowardActiveTeam &&
                                 roster.ContainsKey(member.StudentId) &&
                                 !blocked.Contains(member.StudentId))
                .OrderBy(member => member.JoinedAt)
                .ThenBy(member => roster[member.StudentId].SortKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (continuations.TryGetValue(source.TeamLineageId, out var continuation))
            {
                if (candidates.Count == 0) continue;

                if (continuation.TargetClassId != targetClass.Id)
                {
                    items.Add(Item(source, null, TeamContinuationOutcomes.ContinuedElsewhere, 0,
                        "This team was already continued in another class of this semester."));
                    continue;
                }

                if (continuation.Status == TeamContinuationStatus.Dissolved ||
                    !continuation.CreatedTeamId.HasValue ||
                    !continuedTeams.TryGetValue(continuation.CreatedTeamId.Value, out var continuedTeam))
                {
                    items.Add(Item(source, null, TeamContinuationOutcomes.Dissolved, 0,
                        "The continued team was dissolved and is not recreated automatically."));
                    continue;
                }

                var alreadyAdded = continuation.Members.Select(member => member.StudentId).ToHashSet();
                var capacity = TeamEligibilityRules.MaximumMembers -
                               continuedTeam.TeamMembers.Count(member => member.CountsTowardActiveTeam);
                var toAdd = candidates
                    .Where(member => !alreadyAdded.Contains(member.StudentId))
                    .Take(Math.Max(capacity, 0))
                    .ToList();
                if (toAdd.Count == 0) continue;

                if (apply)
                {
                    foreach (var member in toAdd)
                    {
                        var enrollment = roster[member.StudentId].Enrollment!;
                        continuedTeam.TeamMembers.Add(NewTeamMember(continuedTeam, enrollment, TeamMemberRole.Member, now, actorUserId));
                        continuation.Members.Add(new TeamContinuationMember
                        {
                            ContinuationId = continuation.Id,
                            StudentId = member.StudentId,
                            AddedAtUtc = now
                        });
                        blocked.Add(member.StudentId);
                    }
                    changed = true;
                }

                items.Add(Item(source, continuedTeam, TeamContinuationOutcomes.MembersAdded,
                    continuedTeam.TeamMembers.Count(member => member.CountsTowardActiveTeam) + (apply ? 0 : toAdd.Count)));
                continue;
            }

            if (candidates.Count == 0) continue;

            var eligibility = TeamEligibilityRules.Evaluate(
                candidates.Select(member => roster[member.StudentId].MajorCode).ToArray());
            if (!eligibility.IsEligible)
            {
                items.Add(Item(source, null, TeamContinuationOutcomes.NotEligible, candidates.Count,
                    eligibility.Reasons.ToArray()));
                continue;
            }

            var teamName = ResolveTeamName(source.TeamName, previousSemester.Code, teamNames, proposalNames);
            teamNames.Add(teamName);

            if (!apply)
            {
                items.Add(new TeamContinuationItemDto
                {
                    SourceTeamId = source.Id,
                    TeamName = teamName,
                    SourceTeamName = source.TeamName,
                    SourceClassCode = source.Class.ClassCode,
                    Outcome = TeamContinuationOutcomes.Created,
                    MemberCount = candidates.Count
                });
                foreach (var member in candidates) blocked.Add(member.StudentId);
                continue;
            }

            var team = CreateContinuedTeam(
                source, targetClass, teamName, codePrefix, ++nextSequence, actorUserId, now, candidates, roster);
            _context.Teams.Add(team);
            _context.TeamContinuations.Add(new TeamContinuation
            {
                TeamLineageId = source.TeamLineageId,
                SourceTeamId = source.Id,
                TargetSemesterId = targetClass.SemesterId,
                TargetClassId = targetClass.Id,
                CreatedTeamId = team.Id,
                Status = TeamContinuationStatus.Active,
                CreatedAtUtc = now,
                Members = candidates.Select(member => new TeamContinuationMember
                {
                    StudentId = member.StudentId,
                    AddedAtUtc = now
                }).ToList()
            });

            EnqueueTeamEvents(targetClass, team, source, previousSemester.Code, roster, now);
            foreach (var member in candidates) blocked.Add(member.StudentId);
            changed = true;
            items.Add(Item(source, team, TeamContinuationOutcomes.Created, candidates.Count));
        }

        if (apply && changed)
        {
            _context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = targetClass.Id,
                Action = "TEAM_CONTINUITY_APPLIED",
                PerformedByUserId = actorUserId,
                OccurredAtUtc = now,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    SourceSemesterCode = previousSemester.Code,
                    CreatedCount = items.Count(item => item.Outcome == TeamContinuationOutcomes.Created),
                    MembersAddedCount = items.Count(item => item.Outcome == TeamContinuationOutcomes.MembersAdded)
                })
            });
            await _context.SaveChangesAsync(ct);
        }

        return new TeamContinuationSummaryDto
        {
            SourceSemesterCode = previousSemester.Code,
            CreatedCount = items.Count(item => item.Outcome == TeamContinuationOutcomes.Created),
            MembersAddedCount = items.Count(item => item.Outcome == TeamContinuationOutcomes.MembersAdded),
            NotEligibleCount = items.Count(item => item.Outcome == TeamContinuationOutcomes.NotEligible),
            Items = items
        };
    }

    private sealed record SemesterInfo(Guid Id, string Code, DateOnly SortDate);

    private async Task<SemesterInfo?> FindPreviousSemesterAsync(Guid targetSemesterId, CancellationToken ct)
    {
        var semesters = (await _context.Semesters.AsNoTracking()
                .Select(semester => new { semester.Id, semester.Code, semester.StartDate, semester.Year, semester.Term })
                .ToListAsync(ct))
            .Select(semester => new SemesterInfo(
                semester.Id,
                semester.Code,
                semester.StartDate ?? new DateOnly(
                    Math.Clamp(semester.Year, 1, 9998),
                    semester.Term switch { SemesterTerm.Spring => 1, SemesterTerm.Summer => 5, _ => 9 },
                    1)))
            .ToList();

        var target = semesters.FirstOrDefault(semester => semester.Id == targetSemesterId);
        if (target == null) return null;

        return semesters
            .Where(semester => semester.Id != target.Id && semester.SortDate < target.SortDate)
            .OrderByDescending(semester => semester.SortDate)
            .FirstOrDefault();
    }

    private async Task<Dictionary<Guid, RosterStudent>> LoadRosterAsync(
        Guid classId,
        IReadOnlyCollection<PendingContinuationStudent> pendingStudents,
        bool tracking,
        CancellationToken ct)
    {
        var query = _context.ClassStudents
            .Include(enrollment => enrollment.Student)
            .Where(enrollment => enrollment.ClassId == classId && enrollment.EnrollmentStatus == EnrollmentStatus.Active);
        if (!tracking) query = query.AsNoTracking();
        var enrollments = await query.ToListAsync(ct);

        var registeredMajors = await RegisteredStudentMajorResolver.LoadByEmailAsync(
            _context, enrollments.Select(enrollment => enrollment.Student.Email), ct);

        var roster = new Dictionary<Guid, RosterStudent>();
        foreach (var enrollment in enrollments)
        {
            var major = StudentEnrollmentRules.ResolveEffectiveMajorCode(
                enrollment.MajorCodeAtEnrollment, enrollment.Student.MajorCode);
            if (!IsValidMajor(major) &&
                !string.IsNullOrWhiteSpace(enrollment.Student.Email) &&
                registeredMajors.TryGetValue(enrollment.Student.Email, out var registeredMajor))
            {
                major = registeredMajor;
            }

            roster[enrollment.StudentId] = new RosterStudent(
                enrollment.StudentId,
                major,
                enrollment,
                enrollment.Student.RollNumber ?? string.Empty);
        }

        if (pendingStudents.Count > 0)
        {
            var codes = pendingStudents
                .Select(pending => pending.StudentCode.Trim().ToUpperInvariant())
                .Where(code => code.Length > 0)
                .Distinct()
                .ToArray();
            var emails = pendingStudents
                .Select(pending => pending.Email.Trim().ToLowerInvariant())
                .Where(email => email.Length > 0)
                .Distinct()
                .ToArray();
            var profiles = await _context.Students.AsNoTracking()
                .Where(student =>
                    (student.NormalizedRollNumber != null && codes.Contains(student.NormalizedRollNumber)) ||
                    (student.Email != null && emails.Contains(student.Email.ToLower())))
                .Select(student => new { student.Id, student.NormalizedRollNumber, student.Email, student.MajorCode, student.RollNumber })
                .ToListAsync(ct);

            foreach (var pending in pendingStudents)
            {
                var code = pending.StudentCode.Trim().ToUpperInvariant();
                var email = pending.Email.Trim().ToLowerInvariant();
                var byCode = profiles.Where(profile => profile.NormalizedRollNumber == code).ToList();
                var matches = byCode.Count > 0
                    ? byCode
                    : profiles.Where(profile => profile.Email != null && profile.Email.ToLowerInvariant() == email).ToList();
                // Ambiguous or unknown rows are skipped: they cannot be a former team member.
                if (matches.Count != 1 || roster.ContainsKey(matches[0].Id)) continue;

                roster[matches[0].Id] = new RosterStudent(
                    matches[0].Id,
                    StudentEnrollmentRules.ResolveEffectiveMajorCode(pending.MajorCode, matches[0].MajorCode),
                    null,
                    matches[0].RollNumber ?? string.Empty);
            }
        }

        return roster;
    }

    private static bool IsValidMajor(string? major) =>
        TeamMajorCompositionRules.IsGroupOne(major) || TeamMajorCompositionRules.IsGroupTwo(major);

    private async Task<HashSet<Guid>> LoadBlockedStudentIdsAsync(
        Guid classId,
        Guid[] studentIds,
        CancellationToken ct)
    {
        var blocked = new HashSet<Guid>(await _context.TeamMembers.AsNoTracking()
            .Where(member => member.ClassId == classId && member.CountsTowardActiveTeam && studentIds.Contains(member.StudentId))
            .Select(member => member.StudentId)
            .ToListAsync(ct));

        blocked.UnionWith(await _context.TeamProposalMembers.AsNoTracking()
            .Where(member => member.ClassId == classId && member.CountsTowardOpenProposal && studentIds.Contains(member.StudentId))
            .Select(member => member.StudentId)
            .ToListAsync(ct));

        blocked.UnionWith(await _context.TeamFormationInvitations.AsNoTracking()
            .Where(invitation =>
                invitation.ClassId == classId &&
                studentIds.Contains(invitation.StudentId) &&
                invitation.ReservationReleasedAtUtc == null &&
                invitation.Formation.Status == TeamFormationStatus.Pending)
            .Select(invitation => invitation.StudentId)
            .ToListAsync(ct));

        return blocked;
    }

    private static Team CreateContinuedTeam(
        Team source,
        Class targetClass,
        string teamName,
        string codePrefix,
        int sequence,
        Guid actorUserId,
        DateTime now,
        IReadOnlyList<TeamMember> candidates,
        IReadOnlyDictionary<Guid, RosterStudent> roster)
    {
        var team = new Team
        {
            ClassId = targetClass.Id,
            TeamCode = $"{codePrefix}{sequence}",
            TeamName = teamName,
            Description = source.Description,
            Status = TeamStatus.Active,
            CreatedById = actorUserId,
            CreatedBy = actorUserId,
            CreatedAt = now,
            TeamLineageId = source.TeamLineageId,
            PreviousTeamId = source.Id
        };

        // Keep the previous leader; otherwise the member who joined the team first
        // (candidates are already ordered by JoinedAt, then roll number).
        var leaderId = candidates.FirstOrDefault(member => member.RoleInTeam == TeamMemberRole.Leader)?.StudentId
                       ?? candidates[0].StudentId;
        foreach (var candidate in candidates)
        {
            team.TeamMembers.Add(NewTeamMember(
                team,
                roster[candidate.StudentId].Enrollment!,
                candidate.StudentId == leaderId ? TeamMemberRole.Leader : TeamMemberRole.Member,
                now,
                actorUserId));
        }

        if (source.Project != null)
        {
            var sourceProject = source.Project;
            var project = new Project
            {
                TeamId = team.Id,
                Team = team,
                Name = sourceProject.Name,
                Description = sourceProject.Description,
                Problem = sourceProject.Problem,
                Solution = sourceProject.Solution,
                TargetUsers = sourceProject.TargetUsers,
                ZaloGroupUrl = sourceProject.ZaloGroupUrl,
                StartupField = sourceProject.StartupField,
                BusinessModel = sourceProject.BusinessModel,
                Technology = sourceProject.Technology,
                Status = sourceProject.Status,
                IsHighPotential = sourceProject.IsHighPotential,
                IsFunded = sourceProject.IsFunded,
                IsAwarded = sourceProject.IsAwarded,
                AchievementNote = sourceProject.AchievementNote,
                AchievementsUpdatedAt = sourceProject.AchievementsUpdatedAt,
                AchievementsUpdatedBy = sourceProject.AchievementsUpdatedBy,
                SubmittedAt = sourceProject.SubmittedAt,
                CreatedById = actorUserId,
                CreatedBy = actorUserId,
                CreatedAt = now,
                ProjectLineageId = sourceProject.ProjectLineageId,
                PreviousProjectId = sourceProject.Id
            };
            foreach (var tag in sourceProject.ProjectTags)
            {
                project.ProjectTags.Add(new ProjectTag
                {
                    ProjectId = project.Id,
                    Project = project,
                    TagName = tag.TagName,
                    NormalizedTagName = tag.NormalizedTagName,
                    TagType = tag.TagType,
                    CreatedById = actorUserId,
                    CreatedAt = now
                });
            }

            project.ActivityLogs.Add(new ProjectActivityLog
            {
                ProjectId = project.Id,
                Project = project,
                ActorUserId = actorUserId,
                Action = "CONTINUED_FROM_PREVIOUS_SEMESTER",
                Summary = $"Continued the project from team '{source.TeamName}' of class {source.Class.ClassCode}.",
                OccurredAtUtc = now
            });
            var carriedOver = Features.ProjectData.Common.ProjectAchievementMapping.ToNames(sourceProject);
            if (carriedOver.Count > 0)
            {
                project.ActivityLogs.Add(new ProjectActivityLog
                {
                    ProjectId = project.Id,
                    Project = project,
                    ActorUserId = actorUserId,
                    Action = Features.ProjectData.Common.ProjectAchievementMapping.CarriedOverAction,
                    Summary = $"Carried over achievements ({string.Join(", ", carriedOver)}) from the previous semester.",
                    ChangedFieldsJson = System.Text.Json.JsonSerializer.Serialize(
                        Features.ProjectData.Common.ProjectAchievementHistoryParser.BuildCarriedOverTokens(
                            carriedOver, sourceProject.AchievementNote)),
                    OccurredAtUtc = now
                });
            }
            team.Project = project;
        }

        return team;
    }

    private static TeamMember NewTeamMember(
        Team team,
        ClassStudent enrollment,
        TeamMemberRole role,
        DateTime now,
        Guid actorUserId) => new()
    {
        TeamId = team.Id,
        Team = team,
        ClassId = enrollment.ClassId,
        StudentId = enrollment.StudentId,
        ClassStudent = enrollment,
        RoleInTeam = role,
        CountsTowardActiveTeam = true,
        JoinedAt = now,
        CreatedById = actorUserId
    };

    private void EnqueueTeamEvents(
        Class targetClass,
        Team team,
        Team source,
        string sourceSemesterCode,
        IReadOnlyDictionary<Guid, RosterStudent> roster,
        DateTime now)
    {
        var memberUserIds = team.TeamMembers
            .Select(member => roster[member.StudentId].Enrollment!.Student.UserId)
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToArray();

        ClassOutbox.Enqueue(_context, "Team.Created.v1", targetClass.Id, new
        {
            TeamId = team.Id,
            team.TeamName,
            StudentUserIds = memberUserIds
        }, now);

        if (team.Project != null)
        {
            ClassOutbox.Enqueue(_context, "ProjectWorkspace.Created.v1", targetClass.Id, new
            {
                ProjectId = team.Project.Id,
                TeamId = team.Id,
                ClassId = targetClass.Id,
                SubjectId = targetClass.CourseId,
                SemesterId = targetClass.SemesterId,
                LeaderUserId = (Guid?)null
            }, now);
        }

        ClassOutbox.Enqueue(_context, "Team.ContinuedFromPreviousSemester.v1", targetClass.Id, new
        {
            TeamId = team.Id,
            PreviousTeamId = source.Id,
            TeamLineageId = team.TeamLineageId,
            SourceSemesterCode = sourceSemesterCode
        }, now);
    }

    private static TeamContinuationItemDto Item(
        Team source,
        Team? team,
        string outcome,
        int memberCount,
        params string[] reasons) => new()
    {
        TeamId = team?.Id,
        SourceTeamId = source.Id,
        TeamName = team?.TeamName ?? source.TeamName,
        SourceTeamName = source.TeamName,
        SourceClassCode = source.Class.ClassCode,
        Outcome = outcome,
        MemberCount = memberCount,
        Reasons = reasons
    };

    private static string ResolveTeamName(
        string sourceName,
        string sourceSemesterCode,
        ISet<string> existingTeamNames,
        ISet<string> existingProposalNames)
    {
        bool Taken(string name) => existingTeamNames.Contains(name) || existingProposalNames.Contains(name);

        var baseName = sourceName.Trim();
        if (baseName.Length > MaximumTeamNameLength) baseName = baseName[..MaximumTeamNameLength];
        if (!Taken(baseName)) return baseName;

        var suffix = $" ({sourceSemesterCode})";
        var candidate = Truncate(baseName, MaximumTeamNameLength - suffix.Length) + suffix;
        for (var counter = 2; Taken(candidate); counter++)
        {
            var numbered = $"{suffix[..^1]} #{counter})";
            candidate = Truncate(baseName, MaximumTeamNameLength - numbered.Length) + numbered;
        }

        return candidate;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static string CreateTeamCodePrefix(string classCode)
    {
        var maximumClassCodeLength = MaximumTeamCodeLength - CodeSuffixPrefix.Length - 10;
        var normalized = classCode.Trim();
        if (normalized.Length > maximumClassCodeLength) normalized = normalized[..maximumClassCodeLength];
        return $"{normalized}{CodeSuffixPrefix}";
    }

    private static int GetHighestTeamSequence(IEnumerable<string> codes, string prefix)
    {
        var highest = 0;
        foreach (var code in codes)
        {
            if (code.Length > prefix.Length &&
                int.TryParse(code[prefix.Length..], out var sequence) &&
                sequence > highest)
            {
                highest = sequence;
            }
        }

        return highest;
    }
}
