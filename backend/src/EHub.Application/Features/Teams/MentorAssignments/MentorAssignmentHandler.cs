using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Exceptions;
using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.Teams.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.MentorAssignments;

public sealed class MentorAssignmentHandler : IMentorAssignmentHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public MentorAssignmentHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyCollection<MentorCandidateDto>>> GetCandidatesAsync(
        Guid classId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null)
            return Result.Failure<IReadOnlyCollection<MentorCandidateDto>>(new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));

        var isAdmin = IsRole(role, SystemRoles.Admin);
        var isAssignedLecturer = IsRole(role, SystemRoles.Lecturer) && targetClass.PrimaryLecturerId == userId;
        if (!isAdmin && !isAssignedLecturer)
            return Result.Failure<IReadOnlyCollection<MentorCandidateDto>>(new Error(ErrorCodes.ClassAccessDenied, "You cannot view mentor candidates for this class."));

        var candidates = await _context.MentorProfiles.AsNoTracking()
            .Where(profile =>
                profile.Status == MentorProfileStatus.Active &&
                profile.User.Status == UserStatus.Active &&
                _context.SemesterStaffAssignments.Any(staff =>
                    staff.SemesterId == targetClass.SemesterId &&
                    staff.UserId == profile.UserId &&
                    staff.Role == SemesterStaffRole.Mentor &&
                    staff.Status == SemesterStaffStatus.Active))
            .OrderBy(profile => profile.User.FullName)
            .Select(profile => new MentorCandidateDto
            {
                Mentor = new MentorSummaryDto
                {
                    MentorProfileId = profile.Id,
                    UserId = profile.UserId,
                    FullName = profile.User.FullName,
                    Email = profile.User.Email,
                    Organization = profile.Organization,
                    MentorType = profile.Type.ToString(),
                    Department = profile.Department,
                    JobTitle = profile.JobTitle,
                    ContractType = profile.ContractType,
                    Tags = new EHub.Contracts.Mentors.MentorTagsDto
                    {
                        Expertise = profile.Expertise,
                        StartupDomains = profile.StartupDomains,
                        TechnologySkills = profile.TechnologySkills,
                        MentorTags = profile.MentorTags
                    }
                },
                ActiveTeamCount = profile.Assignments.Count(assignment => assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null && assignment.Team.Class.SemesterId == targetClass.SemesterId)
            })
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<MentorCandidateDto>>(candidates);
    }

    public async Task<Result<IReadOnlyCollection<MentorAssignmentDto>>> GetForClassAsync(
        Guid classId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null) return FailureList(ErrorCodes.ClassNotFound, "The requested class was not found.");
        var isAdmin = IsRole(role, SystemRoles.Admin);
        var isAssignedLecturer = IsRole(role, SystemRoles.Lecturer) && targetClass.PrimaryLecturerId == userId;
        if (!isAdmin && !isAssignedLecturer)
            return FailureList(ErrorCodes.ClassAccessDenied, "You cannot view mentor assignments for this class.");

        var assignments = await AssignmentQuery()
            .Where(item => item.Team.ClassId == classId && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
            .OrderBy(item => item.Team.TeamCode)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<MentorAssignmentDto>>(assignments.Select(TeamMappings.ToMentorAssignmentDto).ToArray());
    }

    public async Task<Result<IReadOnlyCollection<MentorAssignmentDto>>> GetForTeamAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var team = await _context.Teams.AsNoTracking().Include(item => item.Class)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null) return FailureList(ErrorCodes.TeamNotFound, "The requested team was not found.");

        IQueryable<MentorAssignment> query = AssignmentQuery().Where(item => item.TeamId == teamId);
        if (IsRole(role, SystemRoles.Lecturer))
        {
            if (team.Class.PrimaryLecturerId != userId)
                return FailureList(ErrorCodes.ClassAccessDenied, "You cannot view this team's mentor history.");
        }
        else if (IsRole(role, SystemRoles.Mentor))
        {
            var mentorProfileId = await _context.MentorProfiles.AsNoTracking()
                .Where(profile => profile.UserId == userId)
                .Select(profile => (Guid?)profile.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!mentorProfileId.HasValue || !await _context.MentorAssignments.AsNoTracking().AnyAsync(item =>
                    item.TeamId == teamId && item.MentorProfileId == mentorProfileId &&
                    item.Status == MentorAssignmentStatus.Active && item.EndedAt == null, cancellationToken))
                return FailureList(ErrorCodes.ClassAccessDenied, "You can only view a team currently assigned to you.");
            query = query.Where(item => item.MentorProfileId == mentorProfileId.Value);
        }
        else if (!IsRole(role, SystemRoles.Admin))
        {
            return FailureList(ErrorCodes.ClassAccessDenied, "You cannot view mentor assignments for this team.");
        }

        var assignments = await query.OrderByDescending(item => item.AssignedAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<MentorAssignmentDto>>(assignments.Select(TeamMappings.ToMentorAssignmentDto).ToArray());
    }

    public async Task<Result<MentorAssignmentDto>> AssignAsync(
        Guid teamId, AssignMentorRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (request.MentorProfileId == Guid.Empty)
            return Failure(ErrorCodes.ClassValidationError, "Mentor profile id is required.");
        if ((request.Note?.Length ?? 0) > 1_000)
            return Failure(ErrorCodes.ClassValidationError, "Assignment note cannot exceed 1000 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var team = await _context.Teams.Include(item => item.Class)
                    .FirstOrDefaultAsync(item => item.Id == teamId, transactionCancellationToken);
                if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");

                var isAdmin = IsRole(role, SystemRoles.Admin);
                var isAssignedLecturer = IsRole(role, SystemRoles.Lecturer) && team.Class.PrimaryLecturerId == userId;
                if (!isAdmin && !isAssignedLecturer)
                    return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can assign a mentor.");

                if (team.Status != TeamStatus.Active)
                    return Failure(ErrorCodes.TeamInactive, "A mentor can only be assigned to an active team.");
                var mutationError = ClassStateRules.GetMutationError(team.Class.Status);
                if (mutationError != null) return Failure(mutationError.Code, mutationError.Message);

                var mentor = await _context.MentorProfiles.Include(profile => profile.User)
                    .FirstOrDefaultAsync(profile => profile.Id == request.MentorProfileId, transactionCancellationToken);
                if (mentor == null || mentor.Status != MentorProfileStatus.Active || mentor.User.Status != UserStatus.Active)
                    return Failure(ErrorCodes.MentorNotAvailable, "The selected mentor is not available.");

                var isListedForSemester = await _context.SemesterStaffAssignments
                    .AsNoTracking()
                    .AnyAsync(
                        item =>
                            item.SemesterId == team.Class.SemesterId &&
                            item.UserId == mentor.UserId &&
                            item.Role == SemesterStaffRole.Mentor &&
                            item.Status == SemesterStaffStatus.Active,
                        transactionCancellationToken);
                if (!isListedForSemester)
                {
                    return Failure(
                        ErrorCodes.MentorNotAvailable,
                        "The selected mentor is not active in this semester's teaching staff list.");
                }

                var current = await _context.MentorAssignments
                    .Include(item => item.Team).Include(item => item.MentorProfile).ThenInclude(profile => profile.User)
                    .Where(item => item.TeamId == teamId && item.Slot == mentor.Type && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
                    .ToListAsync(transactionCancellationToken);
                var same = current.FirstOrDefault(item => item.MentorProfileId == mentor.Id);
                if (same != null) return Result.Success(TeamMappings.ToMentorAssignmentDto(same));

                if (current.Count > 0)
                {
                    return Failure(
                        ErrorCodes.MentorAssignmentConflict,
                        $"This team already has an active {mentor.Type} mentor. End the current assignment before assigning a replacement.");
                }

                var now = DateTime.UtcNow;
                var assignment = new MentorAssignment
                {
                    MentorProfileId = mentor.Id,
                    MentorProfile = mentor,
                    TeamId = team.Id,
                    Team = team,
                    AssignedById = userId,
                    AssignedAt = now,
                    Slot = mentor.Type,
                    Status = MentorAssignmentStatus.Active,
                    Note = request.Note?.Trim(),
                    CreatedBy = userId
                };
                _context.MentorAssignments.Add(assignment);
                _context.ClassAuditLogs.Add(new ClassAuditLog
                {
                    ClassId = team.ClassId,
                    Action = "MENTOR_ASSIGNED",
                    PerformedByUserId = userId,
                    OccurredAtUtc = now,
                    DetailsJson = JsonSerializer.Serialize(new { TeamId = team.Id, MentorProfileId = mentor.Id, Slot = mentor.Type.ToString() })
                });
                ClassOutbox.Enqueue(_context, "Team.MentorAssignmentChanged.v1", team.ClassId, new
                {
                    TeamId = team.Id,
                    MentorProfileId = mentor.Id,
                    MentorUserId = mentor.UserId,
                    Slot = mentor.Type.ToString(),
                    Action = "Assigned"
                }, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(TeamMappings.ToMentorAssignmentDto(assignment));
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.MentorAssignmentConflict, "The mentor assignment changed concurrently. Refresh and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.MentorAssignmentConflict, "The mentor assignment changed concurrently. Refresh and try again.");
        }
    }

    public async Task<Result> EndAsync(
        Guid teamId, EndMentorAssignmentRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 1_000)
            return Result.Failure(new Error(ErrorCodes.ClassValidationError, "A reason between 3 and 1000 characters is required."));

        if (request.AssignmentId == Guid.Empty)
            return Result.Failure(new Error(ErrorCodes.ClassValidationError, "Assignment id is required."));
        var current = await _context.MentorAssignments.Include(item => item.Team).ThenInclude(item => item.Class)
            .FirstOrDefaultAsync(item => item.Id == request.AssignmentId && item.TeamId == teamId && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null, cancellationToken);
        if (current == null) return Result.Success();

        var isAdmin = IsRole(role, SystemRoles.Admin);
        var isAssignedLecturer = IsRole(role, SystemRoles.Lecturer) && current.Team.Class.PrimaryLecturerId == userId;
        if (!isAdmin && !isAssignedLecturer)
            return Result.Failure(new Error(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can end a mentor assignment."));
        var mutationError = ClassStateRules.GetMutationError(current.Team.Class.Status);
        if (mutationError != null)
            return Result.Failure(mutationError);
        var now = DateTime.UtcNow;
        current.Status = MentorAssignmentStatus.Ended;
        current.EndedAt = now;
        current.Note = string.IsNullOrWhiteSpace(current.Note)
            ? $"Ended: {request.Reason.Trim()}"
            : $"{current.Note}\nEnded: {request.Reason.Trim()}";
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = current.Team.ClassId,
            Action = "MENTOR_ASSIGNMENT_ENDED",
            PerformedByUserId = userId,
            OccurredAtUtc = now,
            DetailsJson = JsonSerializer.Serialize(new { TeamId = teamId, current.MentorProfileId, Slot = current.Slot.ToString(), Reason = request.Reason.Trim() })
        });
        ClassOutbox.Enqueue(_context, "Team.MentorAssignmentChanged.v1", current.Team.ClassId, new { TeamId = teamId, Action = "Ended" }, now);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // Past teams of the signed-in mentor. It lists only their own ended assignments and never opens the team itself,
    // so a mentor who was replaced or whose class finished does not regain access to the workspace.
    public async Task<Result<IReadOnlyCollection<MentorHistoryItemDto>>> GetMyHistoryAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Mentor))
            return Result.Failure<IReadOnlyCollection<MentorHistoryItemDto>>(new Error(ErrorCodes.ClassAccessDenied, "Only a mentor has a mentoring history."));

        var rows = await _context.MentorAssignments.AsNoTracking()
            .Where(item => item.MentorProfile.UserId == userId && item.Status == MentorAssignmentStatus.Ended && item.EndedAt != null)
            .OrderByDescending(item => item.EndedAt)
            .Select(item => new
            {
                item.Id,
                item.TeamId,
                TeamName = item.Team.TeamName,
                ProjectName = item.Team.Project != null ? item.Team.Project.Name : null,
                ClassId = item.Team.ClassId,
                ClassCode = item.Team.Class.ClassCode,
                SubjectCode = item.Team.Class.Course.Code,
                SemesterCode = item.Team.Class.Semester.Code,
                item.Slot,
                item.AssignedAt,
                EndedAt = item.EndedAt!.Value,
                ClassCompletedAt = item.Team.Class.CompletedAtUtc
            })
            .Take(500)
            .ToListAsync(cancellationToken);

        IReadOnlyCollection<MentorHistoryItemDto> history = rows.Select(row => new MentorHistoryItemDto
        {
            AssignmentId = row.Id,
            TeamId = row.TeamId,
            TeamName = row.TeamName,
            ProjectName = row.ProjectName,
            ClassId = row.ClassId,
            ClassCode = row.ClassCode,
            SubjectCode = row.SubjectCode,
            SemesterCode = row.SemesterCode,
            Slot = row.Slot.ToString(),
            AssignedAtUtc = row.AssignedAt,
            EndedAtUtc = row.EndedAt,
            EndedBecause = row.ClassCompletedAt != null && row.EndedAt >= row.ClassCompletedAt ? "ClassCompleted" : "EndedEarly"
        }).ToArray();
        return Result.Success(history);
    }

    public async Task<Result<MentorAssignmentDto>> ReplaceAsync(
        Guid teamId, ReplaceMentorRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (request.AssignmentId == Guid.Empty || request.MentorProfileId == Guid.Empty)
            return Failure(ErrorCodes.ClassValidationError, "The current assignment and the replacement mentor are required.");
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 3 or > 1_000)
            return Failure(ErrorCodes.ClassValidationError, "A reason between 3 and 1000 characters is required.");
        if ((request.Note?.Length ?? 0) > 1_000)
            return Failure(ErrorCodes.ClassValidationError, "Assignment note cannot exceed 1000 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async token =>
            {
                var current = await _context.MentorAssignments
                    .Include(item => item.Team).ThenInclude(item => item.Class)
                    .Include(item => item.MentorProfile).ThenInclude(profile => profile.User)
                    .FirstOrDefaultAsync(item => item.Id == request.AssignmentId && item.TeamId == teamId &&
                        item.Status == MentorAssignmentStatus.Active && item.EndedAt == null, token);
                if (current == null)
                    return Failure(ErrorCodes.MentorAssignmentConflict, "The current mentor assignment is no longer active. Refresh and try again.");

                var team = current.Team;
                if (!CanManage(role, userId, team.Class))
                    return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can replace a mentor.");
                if (team.Status != TeamStatus.Active)
                    return Failure(ErrorCodes.TeamInactive, "A mentor can only be replaced on an active team.");
                var mutationError = ClassStateRules.GetMutationError(team.Class.Status);
                if (mutationError != null) return Failure(mutationError.Code, mutationError.Message);

                var (mentor, mentorError) = await LoadUsableMentorAsync(request.MentorProfileId, team.Class.SemesterId, token);
                if (mentorError != null) return Failure(mentorError.Code, mentorError.Message);
                if (mentor!.Type != current.Slot)
                    return Failure(ErrorCodes.ClassValidationError, "The replacement must be the same type of mentor as the current one.");
                if (mentor.Id == current.MentorProfileId)
                    return Failure(ErrorCodes.ClassValidationError, "The selected mentor is already assigned to this slot.");

                var now = DateTime.UtcNow;
                AddEndEffects(current, reason, userId, now);
                // The old assignment is saved as ended before the new one is added, inside the same transaction.
                await _context.SaveChangesAsync(token);
                var assignment = AddAssignEffects(team, mentor, userId, request.Note, now);
                await _context.SaveChangesAsync(token);
                return Result.Success(TeamMappings.ToMentorAssignmentDto(assignment));
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.MentorAssignmentConflict, "The mentor assignment changed concurrently. Refresh and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.MentorAssignmentConflict, "The mentor assignment changed concurrently. Refresh and try again.");
        }
    }

    public async Task<Result<AssignMentorBatchResponse>> AssignBatchAsync(
        Guid classId, AssignMentorBatchRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        const int maximumTeams = 200;
        var teamIds = request.TeamIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (request.MentorProfileId == Guid.Empty || teamIds.Length == 0)
            return BatchFailure(ErrorCodes.ClassValidationError, "Choose a mentor and at least one team.");
        if (teamIds.Length > maximumTeams)
            return BatchFailure(ErrorCodes.ClassValidationError, $"At most {maximumTeams} teams can be assigned at once.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async token =>
            {
                var targetClass = await _context.Classes.FirstOrDefaultAsync(item => item.Id == classId, token);
                if (targetClass == null) return BatchFailure(ErrorCodes.ClassNotFound, "The requested class was not found.");
                if (!CanManage(role, userId, targetClass))
                    return BatchFailure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can assign a mentor.");
                var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
                if (mutationError != null) return BatchFailure(mutationError.Code, mutationError.Message);

                var (mentor, mentorError) = await LoadUsableMentorAsync(request.MentorProfileId, targetClass.SemesterId, token);
                if (mentorError != null) return BatchFailure(mentorError.Code, mentorError.Message);

                // Only teams of this class are accepted, so a team id from another class can never be reached through it.
                var teams = await _context.Teams.Where(item => item.ClassId == classId && teamIds.Contains(item.Id)).ToListAsync(token);
                if (teams.Count != teamIds.Length)
                    return BatchFailure(ErrorCodes.TeamNotFound, "Every selected team must belong to this class.");

                var occupants = await _context.MentorAssignments
                    .Where(item => teamIds.Contains(item.TeamId) && item.Slot == mentor!.Type &&
                        item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
                    .ToListAsync(token);
                var problems = new List<string>();
                var toAssign = new List<Team>();
                var already = 0;
                foreach (var team in teams.OrderBy(item => item.TeamCode))
                {
                    var occupant = occupants.FirstOrDefault(item => item.TeamId == team.Id);
                    if (team.Status != TeamStatus.Active) problems.Add($"{team.TeamName} is not active");
                    else if (occupant != null && occupant.MentorProfileId == mentor!.Id) already++;
                    else if (occupant != null) problems.Add($"{team.TeamName} already has a {mentor!.Type} mentor");
                    else toAssign.Add(team);
                }
                if (problems.Count > 0)
                {
                    return BatchFailure(
                        ErrorCodes.MentorAssignmentConflict,
                        $"Nothing was saved. {string.Join("; ", problems.Take(5))}{(problems.Count > 5 ? $"; and {problems.Count - 5} more" : string.Empty)}. Refresh and try again.");
                }

                var now = DateTime.UtcNow;
                foreach (var team in toAssign) AddAssignEffects(team, mentor!, userId, null, now);
                await _context.SaveChangesAsync(token);
                return Result.Success(new AssignMentorBatchResponse { AssignedCount = toAssign.Count, AlreadyAssignedCount = already });
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BatchFailure(ErrorCodes.MentorAssignmentConflict, "The mentor assignments changed concurrently. Refresh and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return BatchFailure(ErrorCodes.MentorAssignmentConflict, "The mentor assignments changed concurrently. Refresh and try again.");
        }
    }

    private static bool CanManage(string role, Guid userId, Class targetClass) =>
        IsRole(role, SystemRoles.Admin) || (IsRole(role, SystemRoles.Lecturer) && targetClass.PrimaryLecturerId == userId);

    // A mentor can be used only while their profile and account are active and they are listed as active for the class's semester.
    private async Task<(MentorProfile? Mentor, Error? Error)> LoadUsableMentorAsync(Guid mentorProfileId, Guid semesterId, CancellationToken token)
    {
        var mentor = await _context.MentorProfiles.Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.Id == mentorProfileId, token);
        if (mentor == null || mentor.Status != MentorProfileStatus.Active || mentor.User.Status != UserStatus.Active)
            return (null, new Error(ErrorCodes.MentorNotAvailable, "The selected mentor is not available."));
        var isListed = await _context.SemesterStaffAssignments.AsNoTracking().AnyAsync(item =>
            item.SemesterId == semesterId && item.UserId == mentor.UserId &&
            item.Role == SemesterStaffRole.Mentor && item.Status == SemesterStaffStatus.Active, token);
        return isListed
            ? (mentor, null)
            : (null, new Error(ErrorCodes.MentorNotAvailable, "The selected mentor is not active in this semester's teaching staff list."));
    }

    private MentorAssignment AddAssignEffects(Team team, MentorProfile mentor, Guid userId, string? note, DateTime now)
    {
        var assignment = new MentorAssignment
        {
            MentorProfileId = mentor.Id,
            MentorProfile = mentor,
            TeamId = team.Id,
            Team = team,
            AssignedById = userId,
            AssignedAt = now,
            Slot = mentor.Type,
            Status = MentorAssignmentStatus.Active,
            Note = note?.Trim(),
            CreatedBy = userId
        };
        _context.MentorAssignments.Add(assignment);
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = team.ClassId,
            Action = "MENTOR_ASSIGNED",
            PerformedByUserId = userId,
            OccurredAtUtc = now,
            DetailsJson = JsonSerializer.Serialize(new { TeamId = team.Id, MentorProfileId = mentor.Id, Slot = mentor.Type.ToString() })
        });
        ClassOutbox.Enqueue(_context, "Team.MentorAssignmentChanged.v1", team.ClassId, new
        {
            TeamId = team.Id,
            MentorProfileId = mentor.Id,
            MentorUserId = mentor.UserId,
            Slot = mentor.Type.ToString(),
            Action = "Assigned"
        }, now);
        return assignment;
    }

    private void AddEndEffects(MentorAssignment current, string reason, Guid userId, DateTime now)
    {
        current.Status = MentorAssignmentStatus.Ended;
        current.EndedAt = now;
        current.Note = string.IsNullOrWhiteSpace(current.Note) ? $"Ended: {reason}" : $"{current.Note}\nEnded: {reason}";
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = current.Team.ClassId,
            Action = "MENTOR_ASSIGNMENT_ENDED",
            PerformedByUserId = userId,
            OccurredAtUtc = now,
            DetailsJson = JsonSerializer.Serialize(new { TeamId = current.TeamId, current.MentorProfileId, Slot = current.Slot.ToString(), Reason = reason })
        });
        ClassOutbox.Enqueue(_context, "Team.MentorAssignmentChanged.v1", current.Team.ClassId, new { TeamId = current.TeamId, Action = "Ended" }, now);
    }

    private static Result<AssignMentorBatchResponse> BatchFailure(string code, string message) => Result.Failure<AssignMentorBatchResponse>(new Error(code, message));

    private IQueryable<MentorAssignment> AssignmentQuery() => _context.MentorAssignments.AsNoTracking()
        .Include(item => item.Team)
        .Include(item => item.MentorProfile).ThenInclude(profile => profile.User);

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<MentorAssignmentDto> Failure(string code, string message) => Result.Failure<MentorAssignmentDto>(new Error(code, message));
    private static Result<IReadOnlyCollection<MentorAssignmentDto>> FailureList(string code, string message) => Result.Failure<IReadOnlyCollection<MentorAssignmentDto>>(new Error(code, message));
}
