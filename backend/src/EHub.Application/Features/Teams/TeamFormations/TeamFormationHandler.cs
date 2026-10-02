using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.TeamFormations;

public sealed class TeamFormationHandler : ITeamFormationHandler
{
    private static readonly HashSet<string> GroupOneMajors = new(StringComparer.OrdinalIgnoreCase)
    {
        "BBA_HM", "BBA_IB", "BBA_MC", "BBA_MKT", "BEN", "BBA_TM", "BBA_FIN"
    };
    private static readonly HashSet<string> GroupTwoMajors = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIT_AI", "BIT_GD", "BIT_IA", "BIT_SE"
    };

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public TeamFormationHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TeamFormationDto>> CreateAsync(
        Guid classId, CreateTeamFormationRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var creatorId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!creatorId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can create a formation.");
        var teamName = request.TeamName?.Trim() ?? string.Empty;
        if (teamName.Length is < 3 or > 60)
            return Failure(ErrorCodes.ClassValidationError, "Team name must be between 3 and 60 characters.");
        var ids = request.MemberStudentIds?.ToArray() ?? [];
        if (ids.Length is < 4 or > 6 || ids.Distinct().Count() != ids.Length)
            return Failure(ErrorCodes.ClassValidationError, "Select 4 to 6 unique students.");
        if (!ids.Contains(creatorId.Value) || !ids.Contains(request.LeaderStudentId))
            return Failure(ErrorCodes.ClassValidationError, "Creator and proposed leader must both be selected members.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var targetClass = await _context.Classes.FirstOrDefaultAsync(item => item.Id == classId, ct);
                if (targetClass is null) return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
                var members = await ValidateMembersAsync(classId, ids, request.LeaderStudentId, null, ct);
                if (members.IsFailure) return Failure(members.Error.Code, members.Error.Message);
                var nameError = await ValidateNameAsync(classId, teamName, null, ct);
                if (nameError is not null) return Failure(nameError.Value.Code, nameError.Value.Message);

                var now = DateTime.UtcNow;
                var formation = new TeamFormation
                {
                    ClassId = classId,
                    Class = targetClass,
                    CreatorStudentId = creatorId.Value,
                    ProposedLeaderStudentId = request.LeaderStudentId,
                    TeamName = teamName,
                    NormalizedTeamName = teamName.ToLowerInvariant(),
                    Status = TeamFormationStatus.Pending,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                foreach (var member in members.Value)
                {
                    formation.Invitations.Add(new TeamFormationInvitation
                    {
                        FormationId = formation.Id,
                        Formation = formation,
                        ClassId = classId,
                        StudentId = member.StudentId,
                        ClassStudent = member,
                        Status = member.StudentId == creatorId.Value ? TeamInvitationStatus.Accepted : TeamInvitationStatus.Pending,
                        RespondedAtUtc = member.StudentId == creatorId.Value ? now : null
                    });
                }
                _context.TeamFormations.Add(formation);
                ClassOutbox.Enqueue(_context, "TeamFormation.Invited.v1", classId, new
                {
                    FormationId = formation.Id,
                    StudentIds = ids.Where(id => id != creatorId.Value).ToArray()
                }, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(ToDto(formation, creatorId.Value));
            }, cancellationToken);
        }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamFormationReservationConflict, "A selected student or team name is reserved by another formation."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation conflicted with another request. Refresh and try again."); }
    }

    public async Task<Result<IReadOnlyCollection<TeamFormationDto>>> GetMineAsync(
        Guid? classId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return FailureList(ErrorCodes.ClassAccessDenied, "Only a linked student can view formations.");
        var query = FormationQuery().Where(item => item.CreatorStudentId == studentId.Value ||
            item.Invitations.Any(invitation => invitation.StudentId == studentId.Value));
        if (classId.HasValue) query = query.Where(item => item.ClassId == classId.Value);
        var formations = await query.OrderByDescending(item => item.CreatedAt).ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<TeamFormationDto>>(formations.Select(item => ToDto(item, studentId.Value)).ToArray());
    }

    public async Task<Result<IReadOnlyCollection<TeamFormationDto>>> GetPendingInvitationsAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return FailureList(ErrorCodes.ClassAccessDenied, "Only a linked student can view invitations.");
        var formations = await FormationQuery().Where(item => item.Status == TeamFormationStatus.Pending &&
            item.Invitations.Any(invitation => invitation.StudentId == studentId.Value &&
                invitation.Status == TeamInvitationStatus.Pending && invitation.ReservationReleasedAtUtc == null))
            .OrderByDescending(item => item.CreatedAt).ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<TeamFormationDto>>(formations.Select(item => ToDto(item, studentId.Value)).ToArray());
    }

    public async Task<Result<TeamFormationDto>> GetAsync(
        Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can view formations.");
        var formation = await FormationQuery().FirstOrDefaultAsync(item => item.Id == formationId, cancellationToken);
        if (formation is null) return Failure(ErrorCodes.TeamFormationNotFound, "Formation not found.");
        if (!formation.Invitations.Any(item => item.StudentId == studentId.Value))
            return Failure(ErrorCodes.ClassAccessDenied, "You cannot view this formation.");
        return Result.Success(ToDto(formation, studentId.Value));
    }

    public Task<Result<TeamFormationDto>> AcceptAsync(
        Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default) =>
        RespondAsync(formationId, userId, role, accept: true, cancellationToken);

    public Task<Result<TeamFormationDto>> DeclineAsync(
        Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default) =>
        RespondAsync(formationId, userId, role, accept: false, cancellationToken);

    private async Task<Result<TeamFormationDto>> RespondAsync(
        Guid formationId, Guid userId, string role, bool accept, CancellationToken cancellationToken)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can respond to invitations.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var formation = await FormationQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == formationId, ct);
                if (formation is null) return Failure(ErrorCodes.TeamFormationNotFound, "Formation not found.");
                var invitation = formation.Invitations.SingleOrDefault(item => item.StudentId == studentId.Value);
                if (invitation is null) return Failure(ErrorCodes.ClassAccessDenied, "This invitation is not yours.");
                if (formation.Status != TeamFormationStatus.Pending || invitation.Status != TeamInvitationStatus.Pending ||
                    invitation.ReservationReleasedAtUtc.HasValue)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "This invitation is no longer pending.");

                var now = DateTime.UtcNow;
                if (accept)
                {
                    var ids = formation.Invitations.Select(item => item.StudentId).ToArray();
                    if (!ids.Contains(formation.CreatorStudentId) || !ids.Contains(formation.ProposedLeaderStudentId))
                        return Failure(ErrorCodes.ClassValidationError, "Creator and proposed leader must remain selected members.");
                    var members = await ValidateMembersAsync(formation.ClassId, ids, formation.ProposedLeaderStudentId, formation.Id, ct);
                    if (members.IsFailure) return Failure(members.Error.Code, members.Error.Message);
                    var nameError = await ValidateNameAsync(formation.ClassId, formation.TeamName, formation.Id, ct);
                    if (nameError is not null) return Failure(nameError.Value.Code, nameError.Value.Message);

                    invitation.Status = TeamInvitationStatus.Accepted;
                    invitation.RespondedAtUtc = now;
                    formation.UpdatedAt = now;
                    formation.UpdatedBy = userId;
                    if (formation.Invitations.All(item => item.Status == TeamInvitationStatus.Accepted))
                    {
                        var teamCode = await CreateNextTeamCodeAsync(formation.Class, ct);
                        var team = new Team
                        {
                            ClassId = formation.ClassId,
                            TeamCode = teamCode,
                            TeamName = formation.TeamName,
                            Status = TeamStatus.Active,
                            CreatedById = userId,
                            CreatedBy = userId
                        };
                        foreach (var member in members.Value)
                        {
                            team.TeamMembers.Add(new TeamMember
                            {
                                TeamId = team.Id,
                                Team = team,
                                ClassId = formation.ClassId,
                                StudentId = member.StudentId,
                                ClassStudent = member,
                                RoleInTeam = member.StudentId == formation.ProposedLeaderStudentId
                                    ? TeamMemberRole.Leader : TeamMemberRole.Member,
                                CountsTowardActiveTeam = true,
                                JoinedAt = now,
                                CreatedById = userId
                            });
                        }
                        _context.Teams.Add(team);
                        formation.Status = TeamFormationStatus.Completed;
                        formation.CompletedTeamId = team.Id;
                        formation.CompletedTeam = team;
                        formation.CompletedAtUtc = now;
                        ReleaseReservations(formation, now);
                        ClassOutbox.Enqueue(_context, "Team.Created.v1", formation.ClassId, new
                        {
                            TeamId = team.Id,
                            StudentUserIds = members.Value.Where(item => item.Student.UserId.HasValue)
                                .Select(item => item.Student.UserId!.Value).Distinct().ToArray()
                        }, now);
                        ClassOutbox.Enqueue(_context, "TeamFormation.Completed.v1", formation.ClassId, new
                        {
                            FormationId = formation.Id,
                            StudentIds = ids
                        }, now);
                    }
                    else
                    {
                        ClassOutbox.Enqueue(_context, "TeamFormation.Accepted.v1", formation.ClassId, new
                        {
                            FormationId = formation.Id,
                            CreatorStudentId = formation.CreatorStudentId
                        }, now);
                    }
                }
                else
                {
                    invitation.Status = TeamInvitationStatus.Declined;
                    invitation.RespondedAtUtc = now;
                    formation.Status = TeamFormationStatus.Cancelled;
                    formation.CancelledAtUtc = now;
                    formation.UpdatedAt = now;
                    formation.UpdatedBy = userId;
                    ReleaseReservations(formation, now);
                    ClassOutbox.Enqueue(_context, "TeamFormation.Cancelled.v1", formation.ClassId, new
                    {
                        FormationId = formation.Id,
                        StudentIds = formation.Invitations.Select(item => item.StudentId).ToArray()
                    }, now);
                }
                await _context.SaveChangesAsync(ct);
                return Result.Success(ToDto(formation, studentId.Value));
            }, cancellationToken);
        }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamMembershipConflict, "A selected student joined another team. Refresh and try again."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation changed concurrently. Refresh and try again."); }
    }

    public async Task<Result<TeamFormationDto>> CancelAsync(
        Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can cancel a formation.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var formation = await FormationQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == formationId, ct);
                if (formation is null) return Failure(ErrorCodes.TeamFormationNotFound, "Formation not found.");
                if (formation.CreatorStudentId != studentId.Value)
                    return Failure(ErrorCodes.ClassAccessDenied, "Only the creator can cancel this formation.");
                if (formation.Status != TeamFormationStatus.Pending)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "Only a pending formation can be cancelled.");
                var now = DateTime.UtcNow;
                formation.Status = TeamFormationStatus.Cancelled;
                formation.CancelledAtUtc = now;
                formation.UpdatedAt = now;
                formation.UpdatedBy = userId;
                ReleaseReservations(formation, now);
                ClassOutbox.Enqueue(_context, "TeamFormation.Cancelled.v1", formation.ClassId, new
                {
                    FormationId = formation.Id,
                    StudentIds = formation.Invitations.Select(item => item.StudentId).ToArray()
                }, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(ToDto(formation, studentId.Value));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation changed concurrently. Refresh and try again."); }
    }

    private async Task<Result<List<ClassStudent>>> ValidateMembersAsync(
        Guid classId, IReadOnlyCollection<Guid> ids, Guid leaderId, Guid? currentFormationId, CancellationToken ct)
    {
        if (ids.Count is < 4 or > 6 || ids.Distinct().Count() != ids.Count || !ids.Contains(leaderId))
            return MemberFailure(ErrorCodes.ClassValidationError, "Formation requires 4 to 6 unique members and one selected leader.");
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId, ct);
        if (targetClass is null) return MemberFailure(ErrorCodes.ClassNotFound, "Class not found.");
        var stateError = ClassStateRules.GetMutationError(targetClass.Status);
        if (stateError is not null) return Result.Failure<List<ClassStudent>>(stateError);
        var enrollments = await _context.ClassStudents.Include(item => item.Student)
            .Where(item => item.ClassId == classId && ids.Contains(item.StudentId) && item.EnrollmentStatus == EnrollmentStatus.Active)
            .ToListAsync(ct);
        if (enrollments.Count != ids.Count)
            return MemberFailure(ErrorCodes.ClassValidationError, "All members must be actively enrolled in this class.");
        if (await _context.TeamMembers.AsNoTracking().AnyAsync(item =>
            item.ClassId == classId && ids.Contains(item.StudentId) && item.CountsTowardActiveTeam, ct))
            return MemberFailure(ErrorCodes.TeamMembershipConflict, "A selected student already belongs to a team in this class.");
        if (await _context.TeamProposalMembers.AsNoTracking().AnyAsync(item =>
            item.ClassId == classId && ids.Contains(item.StudentId) && item.CountsTowardOpenProposal, ct))
            return MemberFailure(ErrorCodes.TeamProposalMembershipConflict, "A selected student belongs to an open legacy proposal.");
        if (await _context.TeamFormationInvitations.AsNoTracking().AnyAsync(item =>
            item.ClassId == classId && ids.Contains(item.StudentId) && item.ReservationReleasedAtUtc == null &&
            (!currentFormationId.HasValue || item.FormationId != currentFormationId.Value), ct))
            return MemberFailure(ErrorCodes.TeamFormationReservationConflict, "A selected student is reserved by another formation.");

        var registeredMajors = await RegisteredStudentMajorResolver.LoadByEmailAsync(
            _context, enrollments.Select(item => item.Student.Email), ct);
        var majors = enrollments.Select(item =>
        {
            var major = StudentEnrollmentRules.ResolveEffectiveMajorCode(item.MajorCodeAtEnrollment, item.Student.MajorCode);
            if (!MajorCodes.IsValid(major) && !string.IsNullOrWhiteSpace(item.Student.Email) &&
                registeredMajors.TryGetValue(item.Student.Email, out var registeredMajor))
                major = registeredMajor;
            return major?.Trim();
        }).ToArray();
        if (!majors.Any(major => major is not null && GroupOneMajors.Contains(major)) ||
            !majors.Any(major => major is not null && GroupTwoMajors.Contains(major)))
            return MemberFailure(ErrorCodes.TeamMajorCompositionInvalid, "Team requires at least one Group 1 and one Group 2 major.");
        return Result.Success(enrollments);
    }

    private async Task<(string Code, string Message)?> ValidateNameAsync(
        Guid classId, string teamName, Guid? currentFormationId, CancellationToken ct)
    {
        var normalized = teamName.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 60)
            return (ErrorCodes.ClassValidationError, "Team name must be between 3 and 60 characters.");
        if (await _context.Teams.AsNoTracking().AnyAsync(item =>
            item.ClassId == classId && item.TeamName.ToLower() == normalized, ct) ||
            await _context.TeamProposals.AsNoTracking().AnyAsync(item =>
                item.ClassId == classId && item.TeamName.ToLower() == normalized &&
                item.Status != TeamProposalStatus.Rejected && item.Status != TeamProposalStatus.Cancelled, ct) ||
            await _context.TeamFormations.AsNoTracking().AnyAsync(item =>
                item.ClassId == classId && item.NormalizedTeamName == normalized &&
                item.Status == TeamFormationStatus.Pending &&
                (!currentFormationId.HasValue || item.Id != currentFormationId.Value), ct))
            return (ErrorCodes.TeamNameDuplicated, "A team or pending formation already uses this name in the class.");
        return null;
    }

    private async Task<string> CreateNextTeamCodeAsync(Class targetClass, CancellationToken ct)
    {
        const string suffixPrefix = "_TEAM_";
        var classCode = targetClass.ClassCode.Trim();
        var maximumClassCodeLength = 50 - suffixPrefix.Length - 10;
        if (classCode.Length > maximumClassCodeLength) classCode = classCode[..maximumClassCodeLength];
        var prefix = $"{classCode}{suffixPrefix}";
        var codes = await _context.Teams.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ClassId == targetClass.Id && item.TeamCode.StartsWith(prefix))
            .Select(item => item.TeamCode).ToArrayAsync(ct);
        var highest = codes.Select(code => code[prefix.Length..])
            .Select(suffix => int.TryParse(suffix, out var number) ? number : 0).DefaultIfEmpty().Max();
        return $"{prefix}{highest + 1}";
    }

    private IQueryable<TeamFormation> FormationQuery(bool tracking = false)
    {
        var query = tracking ? _context.TeamFormations.AsQueryable() : _context.TeamFormations.AsNoTracking();
        return query.Include(item => item.Class)
            .Include(item => item.Invitations).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student);
    }

    private async Task<Guid?> GetCurrentStudentIdAsync(Guid userId, string role, CancellationToken ct) =>
        !string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase)
            ? null
            : await _context.Students.AsNoTracking().Where(item => item.UserId == userId)
                .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);

    private static void ReleaseReservations(TeamFormation formation, DateTime now)
    {
        foreach (var invitation in formation.Invitations)
            invitation.ReservationReleasedAtUtc = now;
    }

    private static TeamFormationDto ToDto(TeamFormation formation, Guid myStudentId) => new()
    {
        Id = formation.Id,
        ClassId = formation.ClassId,
        ClassCode = formation.Class.ClassCode,
        TeamName = formation.TeamName,
        CreatorStudentId = formation.CreatorStudentId,
        MyStudentId = myStudentId,
        ProposedLeaderStudentId = formation.ProposedLeaderStudentId,
        Status = formation.Status.ToString(),
        CompletedTeamId = formation.CompletedTeamId,
        CreatedAtUtc = formation.CreatedAt,
        Invitations = formation.Invitations.OrderBy(item => item.ClassStudent.Student.RollNumber)
            .Select(item => new TeamFormationInvitationDto
            {
                StudentId = item.StudentId,
                FullName = item.ClassStudent.Student.FullName,
                RollNumber = item.ClassStudent.Student.RollNumber ?? string.Empty,
                Status = item.Status.ToString(),
                IsCreator = item.StudentId == formation.CreatorStudentId,
                IsProposedLeader = item.StudentId == formation.ProposedLeaderStudentId
            }).ToArray()
    };

    private static Result<List<ClassStudent>> MemberFailure(string code, string message) =>
        Result.Failure<List<ClassStudent>>(new Error(code, message));
    private static Result<TeamFormationDto> Failure(string code, string message) =>
        Result.Failure<TeamFormationDto>(new Error(code, message));
    private static Result<IReadOnlyCollection<TeamFormationDto>> FailureList(string code, string message) =>
        Result.Failure<IReadOnlyCollection<TeamFormationDto>>(new Error(code, message));
}
