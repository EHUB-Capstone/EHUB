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

namespace EHub.Application.Features.Teams.ManageTeams;

public sealed class TeamManagementHandler : ITeamManagementHandler
{
    private static readonly HashSet<string> GroupOneMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BBA_HM",
        "BBA_IB",
        "BBA_MC",
        "BBA_MKT",
        "BEN",
        "BBA_TM"
    };

    private static readonly HashSet<string> GroupTwoMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIT_AI",
        "BIT_GD",
        "BIT_IA",
        "BIT_SE"
    };

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public TeamManagementHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyCollection<TeamDto>>> GetForClassAsync(
        Guid classId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null) return FailureList(ErrorCodes.ClassNotFound, "The requested class was not found.");

        var query = TeamQuery().Where(team => team.ClassId == classId && team.Status == TeamStatus.Active);
        if (IsRole(role, SystemRoles.Lecturer))
        {
            if (targetClass.PrimaryLecturerId != userId) return FailureList(ErrorCodes.ClassAccessDenied, "You can only view teams in classes assigned to you.");
        }
        else if (IsRole(role, SystemRoles.Student))
        {
            var studentId = await GetStudentIdAsync(userId, cancellationToken);
            var expectedStatus = ClassStateRules.IsReadOnly(targetClass.Status)
                ? EnrollmentStatus.Completed
                : EnrollmentStatus.Active;
            if (!studentId.HasValue || !await _context.ClassStudents.AsNoTracking().AnyAsync(item =>
                    item.ClassId == classId && item.StudentId == studentId && item.EnrollmentStatus == expectedStatus, cancellationToken))
                return FailureList(ErrorCodes.ClassAccessDenied, "You do not have access to this class team history.");
        }
        else if (IsRole(role, SystemRoles.Mentor))
        {
            query = query.Where(team => team.MentorAssignments.Any(assignment =>
                assignment.MentorProfile.UserId == userId && assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null));
        }
        else if (!IsRole(role, SystemRoles.Admin))
        {
            return FailureList(ErrorCodes.ClassAccessDenied, "You cannot view teams in this class.");
        }

        var teams = await query.OrderBy(team => team.TeamCode).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<TeamDto>>(teams.Select(TeamMappings.ToDto).ToArray());
    }

    public async Task<Result<IReadOnlyCollection<TeamDto>>> GetAccessibleAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var query = TeamQuery().Where(team => team.Status == TeamStatus.Active &&
            (team.Class.Status == ClassStatus.Draft || team.Class.Status == ClassStatus.Active));
        if (IsRole(role, SystemRoles.Lecturer))
            query = query.Where(team => team.Class.PrimaryLecturerId == userId);
        else if (IsRole(role, SystemRoles.Mentor))
            query = query.Where(team => team.MentorAssignments.Any(assignment =>
                assignment.MentorProfile.UserId == userId && assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null));
        else if (IsRole(role, SystemRoles.Student))
        {
            var studentId = await GetStudentIdAsync(userId, cancellationToken);
            if (!studentId.HasValue) return FailureList(ErrorCodes.ClassAccessDenied, "The account is not linked to a student profile.");
            query = query.Where(team => team.TeamMembers.Any(member => member.StudentId == studentId && member.CountsTowardActiveTeam));
        }
        else if (!IsRole(role, SystemRoles.Admin))
            return FailureList(ErrorCodes.ClassAccessDenied, "You cannot view teams.");

        var teams = await query.OrderBy(team => team.Class.ClassCode).ThenBy(team => team.TeamCode).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<TeamDto>>(teams.Select(TeamMappings.ToDto).ToArray());
    }

    public async Task<Result<TeamDto>> GetAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
        if (!await CanViewTeamAsync(team, userId, role, cancellationToken))
            return Failure(ErrorCodes.ClassAccessDenied, "You cannot view this team.");
        return Result.Success(TeamMappings.ToDto(team));
    }

    public async Task<Result<TeamDto>> CreateAsync(
        Guid classId, CreateTeamRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var targetClass = await _context.Classes.FirstOrDefaultAsync(item => item.Id == classId, transactionCancellationToken);
                if (targetClass == null) return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
                var permission = ValidateManager(targetClass, userId, role);
                if (permission != null) return Failure(permission.Value.Code, permission.Value.Message);
                var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
                if (mutationError != null) return Failure(mutationError.Code, mutationError.Message);

                var composition = await LoadAndValidateCompositionAsync(
                    classId,
                    request.MemberIds,
                    request.LeaderStudentId,
                    null,
                    transactionCancellationToken);
                if (composition.IsFailure) return Failure(composition.Error.Code, composition.Error.Message);
                var teamName = request.TeamName.Trim();
                if (teamName.Length is < 3 or > 100) return Failure(ErrorCodes.ClassValidationError, "Team name must be between 3 and 100 characters.");
                if ((request.Description?.Trim().Length ?? 0) > 1_000) return Failure(ErrorCodes.ClassValidationError, "Team description cannot exceed 1000 characters.");
                if (await _context.Teams.AnyAsync(team => team.ClassId == classId && team.TeamName.ToLower() == teamName.ToLower(), transactionCancellationToken))
                    return Failure(ErrorCodes.TeamNameDuplicated, "A team with this name already exists in the class.");

                var now = DateTime.UtcNow;
                var sequence = await GetNextTeamSequenceAsync(targetClass, transactionCancellationToken);
                var team = new Team
                {
                    ClassId = classId,
                    TeamCode = $"{targetClass.ClassCode}_TEAM_{sequence}",
                    TeamName = teamName,
                    Description = request.Description?.Trim(),
                    Status = TeamStatus.Active,
                    CreatedById = userId,
                    CreatedBy = userId
                };
                foreach (var enrollment in composition.Value)
                {
                    team.TeamMembers.Add(new TeamMember
                    {
                        TeamId = team.Id,
                        Team = team,
                        ClassId = classId,
                        StudentId = enrollment.StudentId,
                        ClassStudent = enrollment,
                        RoleInTeam = enrollment.StudentId == request.LeaderStudentId ? TeamMemberRole.Leader : TeamMemberRole.Member,
                        CountsTowardActiveTeam = true,
                        JoinedAt = now,
                        CreatedById = userId
                    });
                }
                _context.Teams.Add(team);
                RecordTeamActivity(team, "TEAM_CREATED", "Team.Created.v1", userId, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(TeamMappings.ToDto(team));
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.TeamMembershipConflict, "A selected student was assigned to another active team concurrently.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "Team creation conflicted with another request. Refresh and try again.");
        }
    }

    public async Task<Result<GenerateClassTeamResponse>> GenerateAsync(
        Guid classId,
        GenerateClassTeamRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!IsStandardMode(request.Mode))
        {
            return GenerateFailure(
                ErrorCodes.ClassValidationError,
                "Only standard team generation with 4 to 6 members is supported.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var targetClass = await _context.Classes
                    .FirstOrDefaultAsync(item => item.Id == classId, transactionCancellationToken);
                if (targetClass == null)
                {
                    return GenerateFailure(ErrorCodes.ClassNotFound, "The requested class was not found.");
                }

                var permission = ValidateManager(targetClass, userId, role);
                if (permission != null)
                {
                    return GenerateFailure(permission.Value.Code, permission.Value.Message);
                }

                if (targetClass.Status == ClassStatus.Archived)
                {
                    return GenerateFailure(ErrorCodes.ClassArchived, "Cannot create teams in an archived class.");
                }

                var composition = await LoadAndValidateCompositionAsync(
                    classId,
                    request.StudentIds,
                    request.LeaderStudentId,
                    null,
                    transactionCancellationToken);
                if (composition.IsFailure)
                {
                    return GenerateFailure(composition.Error.Code, composition.Error.Message);
                }

                var sequence = await GetNextTeamSequenceAsync(targetClass, transactionCancellationToken);
                var teamName = ResolveTeamName(request.TeamName, sequence);
                if (teamName.Length is < 3 or > 100)
                {
                    return GenerateFailure(
                        ErrorCodes.ClassValidationError,
                        "Team name must be between 3 and 100 characters.");
                }

                if ((request.Description?.Trim().Length ?? 0) > 1_000)
                {
                    return GenerateFailure(
                        ErrorCodes.ClassValidationError,
                        "Team description cannot exceed 1000 characters.");
                }

                var hasDuplicateName = await _context.Teams.AnyAsync(
                    item => item.ClassId == classId && item.TeamName.ToLower() == teamName.ToLower(),
                    transactionCancellationToken);
                if (hasDuplicateName)
                {
                    return GenerateFailure(
                        ErrorCodes.TeamNameDuplicated,
                        "A team with this name already exists in the class.");
                }

                var team = CreateApprovedTeam(
                    classId,
                    $"{targetClass.ClassCode}_TEAM_{sequence}",
                    teamName,
                    request.Description,
                    request.LeaderStudentId,
                    composition.Value,
                    userId);
                _context.Teams.Add(team);

                if (request.MentorId.HasValue)
                {
                    var mentorResult = await AddAssignedClassMentorAsync(
                        team,
                        request.MentorId.Value,
                        userId,
                        transactionCancellationToken);
                    if (mentorResult != null)
                    {
                        return GenerateFailure(mentorResult.Value.Code, mentorResult.Value.Message);
                    }
                }

                var createdAt = DateTime.UtcNow;
                RecordTeamActivity(team, "TEAM_CREATED", "Team.Created.v1", userId, createdAt);
                await _context.SaveChangesAsync(transactionCancellationToken);

                return Result.Success(new GenerateClassTeamResponse
                {
                    Team = TeamMappings.ToDto(team)
                });
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return GenerateFailure(
                ErrorCodes.TeamMembershipConflict,
                "A selected student was assigned by another request. Refresh and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return GenerateFailure(
                ErrorCodes.ClassConcurrencyConflict,
                "Team generation conflicted with another request. Refresh and try again.");
        }
    }

    public async Task<Result<TeamDto>> UpdateMembersAsync(
        Guid teamId, UpdateTeamMembersRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var team = await TeamQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == teamId, transactionCancellationToken);
                if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
                var permission = ValidateManager(team.Class, userId, role);
                if (permission != null) return Failure(permission.Value.Code, permission.Value.Message);
                if (team.Status != TeamStatus.Active)
                    return Failure(ErrorCodes.TeamInactive, "Members cannot be changed for an inactive team.");
                var mutationError = ClassStateRules.GetMutationError(team.Class.Status);
                if (mutationError != null) return Failure(mutationError.Code, mutationError.Message);
                if (team.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The team changed concurrently. Refresh and try again.");

                if (request.TeamName != null)
                {
                    var teamName = request.TeamName.Trim();
                    if (teamName.Length is < 3 or > 100) return Failure(ErrorCodes.ClassValidationError, "Team name must be between 3 and 100 characters.");
                    if (await _context.Teams.AnyAsync(item => item.ClassId == team.ClassId && item.Id != team.Id && item.TeamName.ToLower() == teamName.ToLower(), transactionCancellationToken))
                        return Failure(ErrorCodes.TeamNameDuplicated, "A team with this name already exists in the class.");
                    team.TeamName = teamName;
                }
                if (request.Description != null)
                {
                    if (request.Description.Trim().Length > 1_000) return Failure(ErrorCodes.ClassValidationError, "Team description cannot exceed 1000 characters.");
                    team.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                }

                var composition = await LoadAndValidateCompositionAsync(
                    team.ClassId,
                    request.MemberIds,
                    request.LeaderStudentId,
                    team.Id,
                    transactionCancellationToken);
                if (composition.IsFailure) return Failure(composition.Error.Code, composition.Error.Message);
                var desired = composition.Value.ToDictionary(item => item.StudentId);
                var previousMemberUserIds = GetMemberUserIds(
                    team.TeamMembers.Where(member => member.CountsTowardActiveTeam));
                var now = DateTime.UtcNow;
                var previousLeader = team.TeamMembers.FirstOrDefault(member =>
                    member.CountsTowardActiveTeam &&
                    member.RoleInTeam == TeamMemberRole.Leader &&
                    member.StudentId != request.LeaderStudentId);
                if (previousLeader != null)
                {
                    // The database enforces one active leader with a partial unique index. Demote first
                    // inside this transaction so swapping leaders cannot violate the index mid-batch.
                    previousLeader.RoleInTeam = TeamMemberRole.Member;
                    await _context.SaveChangesAsync(transactionCancellationToken);
                }
                foreach (var existing in team.TeamMembers.Where(member => member.CountsTowardActiveTeam))
                {
                    if (!desired.ContainsKey(existing.StudentId)) existing.CountsTowardActiveTeam = false;
                }
                foreach (var enrollment in composition.Value)
                {
                    var member = team.TeamMembers.FirstOrDefault(item => item.StudentId == enrollment.StudentId);
                    if (member == null)
                    {
                        member = new TeamMember
                        {
                            TeamId = team.Id, Team = team, ClassId = team.ClassId, StudentId = enrollment.StudentId,
                            ClassStudent = enrollment, JoinedAt = now, CreatedById = userId
                        };
                        team.TeamMembers.Add(member);
                    }
                    member.CountsTowardActiveTeam = true;
                    member.RoleInTeam = enrollment.StudentId == request.LeaderStudentId ? TeamMemberRole.Leader : TeamMemberRole.Member;
                }
                team.UpdatedAt = now;
                team.UpdatedBy = userId;
                var currentMemberUserIds = GetMemberUserIds(
                    team.TeamMembers.Where(member => member.CountsTowardActiveTeam));
                RecordTeamActivity(
                    team,
                    "TEAM_MEMBERS_UPDATED",
                    "Team.MembersUpdated.v1",
                    userId,
                    now,
                    previousMemberUserIds.Concat(currentMemberUserIds).Distinct().ToArray());
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(TeamMappings.ToDto(team));
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The team changed concurrently. Refresh and try again."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The team changed concurrently. Refresh and try again."); }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamMembershipConflict, "A selected student belongs to another active team."); }
    }

    public async Task<Result<TeamDto>> AssignLeaderAsync(
        Guid teamId, AssignTeamLeaderRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var team = await TeamQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == teamId, transactionCancellationToken);
                if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
                var permission = ValidateManager(team.Class, userId, role);
                if (permission != null) return Failure(permission.Value.Code, permission.Value.Message);
                if (team.Status != TeamStatus.Active)
                    return Failure(ErrorCodes.TeamInactive, "The leader cannot be changed for an inactive team.");
                var mutationError = ClassStateRules.GetMutationError(team.Class.Status);
                if (mutationError != null) return Failure(mutationError.Code, mutationError.Message);
                if (team.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The team changed concurrently. Refresh and try again.");
                var activeMembers = team.TeamMembers.Where(member => member.CountsTowardActiveTeam).ToArray();
                if (!activeMembers.Any(member => member.StudentId == request.StudentId))
                    return Failure(ErrorCodes.ClassValidationError, "The leader must be an active member of the team.");

                var previousLeader = activeMembers.FirstOrDefault(member =>
                    member.RoleInTeam == TeamMemberRole.Leader && member.StudentId != request.StudentId);
                if (previousLeader != null)
                {
                    previousLeader.RoleInTeam = TeamMemberRole.Member;
                    await _context.SaveChangesAsync(transactionCancellationToken);
                }

                foreach (var member in activeMembers)
                    member.RoleInTeam = member.StudentId == request.StudentId ? TeamMemberRole.Leader : TeamMemberRole.Member;
                team.UpdatedAt = DateTime.UtcNow;
                team.UpdatedBy = userId;
                RecordTeamActivity(
                    team,
                    "TEAM_LEADER_ASSIGNED",
                    "Team.LeaderAssigned.v1",
                    userId,
                    team.UpdatedAt.Value,
                    GetMemberUserIds(activeMembers));
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(TeamMappings.ToDto(team));
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The team changed concurrently. Refresh and try again."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The team changed concurrently. Refresh and try again."); }
    }

    public async Task<Result> DeleteAsync(
        Guid teamId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var team = await TeamQuery(tracking: true)
                    .FirstOrDefaultAsync(item => item.Id == teamId, transactionCancellationToken);
                if (team == null)
                {
                    return Result.Failure(new Error(ErrorCodes.TeamNotFound, "The requested team was not found."));
                }

                var permission = ValidateManager(team.Class, userId, role);
                if (permission != null)
                {
                    return Result.Failure(new Error(permission.Value.Code, permission.Value.Message));
                }

                if (team.Status != TeamStatus.Active)
                {
                    return Result.Failure(new Error(ErrorCodes.TeamInactive, "Only an active team can be deleted."));
                }

                var hasProtectedData = await HasProtectedTeamDataAsync(team.Id, transactionCancellationToken);
                if (hasProtectedData)
                {
                    return Result.Failure(new Error(
                        ErrorCodes.TeamDeletionBlocked,
                        "This team has project, proposal, checkpoint, evaluation, or task data and cannot be deleted. Archive the class or preserve the team instead."));
                }

                var now = DateTime.UtcNow;
                var memberUserIds = GetMemberUserIds(
                    team.TeamMembers.Where(member => member.CountsTowardActiveTeam));
                foreach (var member in team.TeamMembers.Where(item => item.CountsTowardActiveTeam))
                {
                    member.CountsTowardActiveTeam = false;
                    member.RoleInTeam = TeamMemberRole.Member;
                }

                var activeMentorAssignments = await _context.MentorAssignments
                    .Where(item => item.TeamId == team.Id &&
                        item.Status == MentorAssignmentStatus.Active &&
                        item.EndedAt == null)
                    .ToListAsync(transactionCancellationToken);
                foreach (var assignment in activeMentorAssignments)
                {
                    assignment.Status = MentorAssignmentStatus.Ended;
                    assignment.EndedAt = now;
                }

                var teamChatGroups = await _context.ChatGroups
                    .IgnoreQueryFilters()
                    .Where(item => item.TeamId == team.Id && !item.IsDeleted)
                    .ToListAsync(transactionCancellationToken);
                foreach (var chatGroup in teamChatGroups)
                {
                    chatGroup.IsReadOnly = true;
                    chatGroup.UpdatedAt = now;
                    chatGroup.UpdatedBy = userId;
                }

                team.Status = TeamStatus.Archived;
                team.ArchivedAt = now;
                team.UpdatedAt = now;
                team.UpdatedBy = userId;
                RecordTeamActivity(
                    team,
                    "TEAM_ARCHIVED",
                    "Team.Archived.v1",
                    userId,
                    now,
                    memberUserIds);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success();
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(new Error(
                ErrorCodes.ClassConcurrencyConflict,
                "The team changed concurrently. Refresh and try again."));
        }
        catch (SerializableTransactionConflictException)
        {
            return Result.Failure(new Error(
                ErrorCodes.ClassConcurrencyConflict,
                "The team changed concurrently. Refresh and try again."));
        }
    }

    private async Task<Result<List<ClassStudent>>> LoadAndValidateCompositionAsync(
        Guid classId,
        IReadOnlyCollection<Guid> memberIds,
        Guid leaderId,
        Guid? currentTeamId,
        CancellationToken cancellationToken)
    {
        var ids = memberIds.Distinct().ToArray();
        if (ids.Length != memberIds.Count)
        {
            return Result.Failure<List<ClassStudent>>(
                new Error(ErrorCodes.ClassValidationError, "Student IDs must be unique."));
        }

        if (ids.Length is < 4 or > 6)
        {
            return Result.Failure<List<ClassStudent>>(new Error(
                ErrorCodes.ClassValidationError,
                "A team must contain 4 to 6 unique members."));
        }
        if (!ids.Contains(leaderId)) return Result.Failure<List<ClassStudent>>(new Error(ErrorCodes.ClassValidationError, "The leader must be one of the selected members."));
        var enrollments = await _context.ClassStudents.Include(item => item.Student)
            .Where(item => item.ClassId == classId && ids.Contains(item.StudentId) && item.EnrollmentStatus == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
        if (enrollments.Count != ids.Length) return Result.Failure<List<ClassStudent>>(new Error(ErrorCodes.ClassValidationError, "Every team member must have an active enrollment in this class."));
        var conflict = await _context.TeamMembers.AsNoTracking().AnyAsync(member =>
            member.ClassId == classId && ids.Contains(member.StudentId) && member.CountsTowardActiveTeam &&
            (!currentTeamId.HasValue || member.TeamId != currentTeamId.Value), cancellationToken);
        if (conflict) return Result.Failure<List<ClassStudent>>(new Error(ErrorCodes.TeamMembershipConflict, "A selected student already belongs to another active team in this class."));
        if (await _context.TeamProposalMembers.AsNoTracking().AnyAsync(member =>
                member.ClassId == classId && ids.Contains(member.StudentId) && member.CountsTowardOpenProposal, cancellationToken))
            return Result.Failure<List<ClassStudent>>(new Error(ErrorCodes.TeamProposalMembershipConflict, "A selected student belongs to an open team proposal."));
        var majors = enrollments.Select(item => item.MajorCodeAtEnrollment).ToArray();
        if (!majors.Any(IsBusinessMajor) || !majors.Any(IsTechnologyMajor))
            return Result.Failure<List<ClassStudent>>(new Error(ErrorCodes.TeamMajorCompositionInvalid, "A team must include at least one GROUP_1 major and one GROUP_2 major."));
        return Result.Success(enrollments);
    }

    private async Task<int> GetNextTeamSequenceAsync(Class targetClass, CancellationToken cancellationToken)
    {
        var prefix = $"{targetClass.ClassCode}_TEAM_";
        var existingCodes = await _context.Teams
            .IgnoreQueryFilters()
            .Where(item => item.ClassId == targetClass.Id && item.TeamCode.StartsWith(prefix))
            .Select(item => item.TeamCode)
            .ToListAsync(cancellationToken);

        var highestSequence = 0;
        foreach (var code in existingCodes)
        {
            var suffix = code[prefix.Length..];
            if (int.TryParse(suffix, out var value) && value > highestSequence)
            {
                highestSequence = value;
            }
        }

        return highestSequence + 1;
    }

    private static Team CreateApprovedTeam(
        Guid classId,
        string teamCode,
        string teamName,
        string? description,
        Guid leaderStudentId,
        IReadOnlyCollection<ClassStudent> enrollments,
        Guid createdByUserId)
    {
        var now = DateTime.UtcNow;
        var team = new Team
        {
            ClassId = classId,
            TeamCode = teamCode,
            TeamName = teamName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = TeamStatus.Active,
            CreatedById = createdByUserId,
            CreatedBy = createdByUserId
        };

        foreach (var enrollment in enrollments)
        {
            team.TeamMembers.Add(new TeamMember
            {
                TeamId = team.Id,
                Team = team,
                ClassId = classId,
                StudentId = enrollment.StudentId,
                ClassStudent = enrollment,
                RoleInTeam = enrollment.StudentId == leaderStudentId
                    ? TeamMemberRole.Leader
                    : TeamMemberRole.Member,
                CountsTowardActiveTeam = true,
                JoinedAt = now,
                CreatedById = createdByUserId
            });
        }

        return team;
    }

    private async Task<(string Code, string Message)?> AddAssignedClassMentorAsync(
        Team team,
        Guid mentorProfileId,
        Guid assignedByUserId,
        CancellationToken cancellationToken)
    {
        var mentor = await _context.MentorProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == mentorProfileId, cancellationToken);
        if (mentor == null || mentor.Status != MentorProfileStatus.Active || mentor.User.Status != UserStatus.Active)
        {
            return (ErrorCodes.MentorNotAvailable, "The selected mentor is not available.");
        }

        var isAssignedToClass = await _context.MentorAssignments
            .AnyAsync(item => item.MentorProfileId == mentorProfileId &&
                item.Team.ClassId == team.ClassId &&
                item.Status == MentorAssignmentStatus.Active &&
                item.EndedAt == null, cancellationToken);
        if (!isAssignedToClass)
        {
            return (ErrorCodes.MentorNotAvailable, "The selected mentor is not assigned to this class.");
        }

        var activeTeamCount = await _context.MentorAssignments
            .CountAsync(item => item.MentorProfileId == mentorProfileId &&
                item.Status == MentorAssignmentStatus.Active &&
                item.EndedAt == null, cancellationToken);
        if (activeTeamCount >= mentor.MaxTeams)
        {
            return (ErrorCodes.MentorCapacityReached, "The selected mentor has reached the maximum active team capacity.");
        }

        var assignment = new MentorAssignment
        {
            MentorProfileId = mentor.Id,
            MentorProfile = mentor,
            TeamId = team.Id,
            Team = team,
            AssignedById = assignedByUserId,
            AssignedAt = DateTime.UtcNow,
            Status = MentorAssignmentStatus.Active,
            CreatedBy = assignedByUserId
        };
        team.MentorAssignments.Add(assignment);
        _context.MentorAssignments.Add(assignment);
        return null;
    }

    private async Task<bool> HasProtectedTeamDataAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var projectId = await _context.Projects
            .Where(item => item.TeamId == teamId)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (projectId.HasValue)
        {
            return true;
        }

        return await _context.ProjectDirections.AnyAsync(item => item.TeamId == teamId, cancellationToken) ||
               await _context.Milestones.AnyAsync(item => item.TeamId == teamId, cancellationToken) ||
               await _context.SprintTasks.AnyAsync(item => item.TeamId == teamId, cancellationToken) ||
               await _context.WeeklyTasks.AnyAsync(item => item.TeamId == teamId, cancellationToken) ||
               await _context.Submissions.AnyAsync(item => item.TeamId == teamId, cancellationToken);
    }

    private static IReadOnlyCollection<Guid> GetMemberUserIds(IEnumerable<ClassStudent> enrollments)
    {
        return enrollments
            .Where(item => item.Student.UserId.HasValue)
            .Select(item => item.Student.UserId!.Value)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyCollection<Guid> GetMemberUserIds(IEnumerable<TeamMember> members)
    {
        return members
            .Where(member => member.ClassStudent.Student.UserId.HasValue)
            .Select(member => member.ClassStudent.Student.UserId!.Value)
            .Distinct()
            .ToArray();
    }

    private static string ResolveTeamName(string? requestedName, int sequence)
    {
        return string.IsNullOrWhiteSpace(requestedName)
            ? $"Team {sequence}"
            : requestedName.Trim();
    }

    private static bool IsStandardMode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, "standard", StringComparison.OrdinalIgnoreCase);
    }

    private IQueryable<Team> TeamQuery(bool tracking = false)
    {
        var query = tracking ? _context.Teams.AsQueryable() : _context.Teams.AsNoTracking();
        return query.Include(team => team.Class)
            .Include(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(team => team.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile).ThenInclude(profile => profile.User)
            .Include(team => team.Project)
            .Include(team => team.ChatGroups);
    }

    private async Task<bool> CanViewTeamAsync(Team team, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (IsRole(role, SystemRoles.Admin)) return true;
        if (IsRole(role, SystemRoles.Lecturer)) return team.Class.PrimaryLecturerId == userId;
        if (IsRole(role, SystemRoles.Mentor)) return team.MentorAssignments.Any(item => item.MentorProfile.UserId == userId && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null);
        if (IsRole(role, SystemRoles.Student))
        {
            var studentId = await GetStudentIdAsync(userId, cancellationToken);
            return studentId.HasValue && team.TeamMembers.Any(item => item.StudentId == studentId && item.CountsTowardActiveTeam);
        }
        return false;
    }

    private void RecordTeamActivity(
        Team team,
        string auditAction,
        string eventType,
        Guid userId,
        DateTime occurredAt,
        IReadOnlyCollection<Guid>? memberUserIds = null)
    {
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = team.ClassId, Action = auditAction, PerformedByUserId = userId, OccurredAtUtc = occurredAt,
            DetailsJson = JsonSerializer.Serialize(new { TeamId = team.Id, team.TeamName })
        });
        ClassOutbox.Enqueue(_context, eventType, team.ClassId, new
        {
            TeamId = team.Id,
            team.TeamName,
            MemberUserIds = memberUserIds ?? Array.Empty<Guid>()
        }, occurredAt);
    }

    private async Task<Guid?> GetStudentIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await _context.Students.AsNoTracking().Where(student => student.UserId == userId).Select(student => (Guid?)student.Id).FirstOrDefaultAsync(cancellationToken);
    private static (string Code, string Message)? ValidateManager(Class targetClass, Guid userId, string role)
    {
        if (IsRole(role, SystemRoles.Admin)) return null;
        if (IsRole(role, SystemRoles.Lecturer) && targetClass.PrimaryLecturerId == userId) return null;
        return (ErrorCodes.ClassAccessDenied, "Only an administrator or the assigned lecturer can manage this team.");
    }
    private static bool IsBusinessMajor(string? code) =>
        !string.IsNullOrWhiteSpace(code) && GroupOneMajorCodes.Contains(code.Trim());

    private static bool IsTechnologyMajor(string? code) =>
        !string.IsNullOrWhiteSpace(code) && GroupTwoMajorCodes.Contains(code.Trim());

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<TeamDto> Failure(string code, string message) => Result.Failure<TeamDto>(new Error(code, message));
    private static Result<GenerateClassTeamResponse> GenerateFailure(string code, string message) => Result.Failure<GenerateClassTeamResponse>(new Error(code, message));
    private static Result<IReadOnlyCollection<TeamDto>> FailureList(string code, string message) => Result.Failure<IReadOnlyCollection<TeamDto>>(new Error(code, message));

}
