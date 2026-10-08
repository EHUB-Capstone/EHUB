using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
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
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public TeamFormationHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<TeamFormationDto>> CreateAsync(
        Guid classId, CreateTeamFormationRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var creatorId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!creatorId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can create a formation.");
        var teamName = request.TeamName?.Trim() ?? string.Empty;
        if (teamName.Length is < TeamFormationRules.MinTeamNameLength or > TeamFormationRules.MaxTeamNameLength)
            return Failure(ErrorCodes.ClassValidationError, "Team name must be between 3 and 60 characters.");
        var inviteeIds = request.InviteeStudentIds?.ToArray() ?? [];
        if (inviteeIds.Length is < 1 or > TeamFormationRules.MaxMembers - 1 || inviteeIds.Distinct().Count() != inviteeIds.Length)
            return Failure(ErrorCodes.ClassValidationError, "Invite 1 to 5 unique students.");
        if (inviteeIds.Contains(creatorId.Value))
            return Failure(ErrorCodes.ClassValidationError, "The creator is added automatically and must not be invited.");
        if (request.LeaderStudentId != creatorId.Value && !inviteeIds.Contains(request.LeaderStudentId))
            return Failure(ErrorCodes.ClassValidationError, "The proposed leader must be the creator or an invited student.");
        var ids = new[] { creatorId.Value }.Concat(inviteeIds).ToArray();

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var now = _clock.UtcNow;
                var targetClass = await _context.Classes.FirstOrDefaultAsync(item => item.Id == classId, ct);
                if (targetClass is null) return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
                await SweepAsync(classId, now, ct);
                var members = await ValidateEligibilityAsync(classId, ids, null, ct);
                if (members.IsFailure) return Failure(members.Error.Code, members.Error.Message);
                var nameError = await ValidateNameAsync(classId, teamName, null, ct);
                if (nameError is not null) return Failure(nameError.Value.Code, nameError.Value.Message);

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
                    var isCreator = member.StudentId == creatorId.Value;
                    formation.Invitations.Add(NewInvitation(formation, member, isCreator, now));
                }
                _context.TeamFormations.Add(formation);
                EnqueueInvited(formation, inviteeIds, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(await ToDtoAsync(formation, creatorId.Value, now, ct));
            }, cancellationToken);
        }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamFormationReservationConflict, "A selected student or team name is reserved by another formation."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation conflicted with another request. Refresh and try again."); }
    }

    public async Task<Result<TeamFormationDto>> InviteAsync(
        Guid formationId, InviteTeamFormationMembersRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can invite members.");
        var newIds = request.StudentIds?.ToArray() ?? [];
        if (newIds.Length == 0 || newIds.Distinct().Count() != newIds.Length)
            return Failure(ErrorCodes.ClassValidationError, "Select at least one unique student to invite.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var now = _clock.UtcNow;
                var formation = await LoadForUpdateAsync(formationId, now, ct);
                if (formation is null) return Failure(ErrorCodes.TeamFormationNotFound, "Formation not found.");
                if (formation.CreatorStudentId != studentId.Value)
                    return Failure(ErrorCodes.ClassAccessDenied, "Only the creator can invite members.");
                if (formation.Status != TeamFormationStatus.Pending)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "Only a pending formation can invite members.");

                var active = ActiveInvitations(formation).ToArray();
                foreach (var id in newIds)
                {
                    var existing = active.FirstOrDefault(item => item.StudentId == id);
                    if (existing is null) continue;
                    return Failure(ErrorCodes.TeamInvitationConflict, existing.Status == TeamInvitationStatus.Accepted
                        ? "This student already accepted and cannot be re-invited."
                        : "This student already has a pending invitation.");
                }
                if (active.Length + newIds.Length > TeamFormationRules.MaxMembers)
                    return Failure(ErrorCodes.TeamFormationCapacityConflict, "A team can have at most 6 members including pending invitations.");

                var members = await ValidateEligibilityAsync(formation.ClassId, newIds, formation.Id, ct);
                if (members.IsFailure) return Failure(members.Error.Code, members.Error.Message);

                foreach (var member in members.Value)
                    formation.Invitations.Add(NewInvitation(formation, member, isCreator: false, now));
                formation.UpdatedAt = now;
                formation.UpdatedBy = userId;
                EnqueueInvited(formation, newIds, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(await ToDtoAsync(formation, studentId.Value, now, ct));
            }, cancellationToken);
        }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamFormationReservationConflict, "A selected student is reserved by another formation."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation changed concurrently. Refresh and try again."); }
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
        return Result.Success<IReadOnlyCollection<TeamFormationDto>>(await ToDtosAsync(formations, studentId.Value, cancellationToken));
    }

    public async Task<Result<IReadOnlyCollection<TeamFormationDto>>> GetPendingInvitationsAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return FailureList(ErrorCodes.ClassAccessDenied, "Only a linked student can view invitations.");
        var now = _clock.UtcNow;
        var formations = await FormationQuery().Where(item => item.Status == TeamFormationStatus.Pending &&
            item.Invitations.Any(invitation => invitation.StudentId == studentId.Value &&
                invitation.Status == TeamInvitationStatus.Pending && invitation.ReservationReleasedAtUtc == null &&
                (invitation.ExpiresAtUtc == null || invitation.ExpiresAtUtc > now)))
            .OrderByDescending(item => item.CreatedAt).ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<TeamFormationDto>>(await ToDtosAsync(formations, studentId.Value, cancellationToken));
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
        return Result.Success(await ToDtoAsync(formation, studentId.Value, _clock.UtcNow, cancellationToken));
    }

    public async Task<Result<TeamFormationDto>> AcceptAsync(
        Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can respond to invitations.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var now = _clock.UtcNow;
                var found = await FindOwnInvitationAsync(formationId, studentId.Value, now, ct);
                if (found.IsFailure) return Failure(found.Error.Code, found.Error.Message);
                var (formation, invitation) = found.Value;
                if (formation.Status != TeamFormationStatus.Pending)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "This invitation is no longer pending.");
                if (invitation.Status == TeamInvitationStatus.Expired)
                    return Failure(ErrorCodes.TeamInvitationExpired, "This invitation has expired. Ask the creator to invite you again.");
                if (invitation.Status != TeamInvitationStatus.Pending || invitation.ReservationReleasedAtUtc.HasValue)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "This invitation is no longer pending.");

                var eligible = await ValidateEligibilityAsync(formation.ClassId, [studentId.Value], formation.Id, ct);
                if (eligible.IsFailure) return Failure(eligible.Error.Code, eligible.Error.Message);

                invitation.Status = TeamInvitationStatus.Accepted;
                invitation.RespondedAtUtc = now;
                formation.UpdatedAt = now;
                formation.UpdatedBy = userId;
                ClassOutbox.Enqueue(_context, "TeamFormation.Accepted.v1", formation.ClassId, new
                {
                    FormationId = formation.Id,
                    CreatorStudentId = formation.CreatorStudentId
                }, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(await ToDtoAsync(formation, studentId.Value, now, ct));
            }, cancellationToken);
        }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamMembershipConflict, "You joined another team. Refresh and try again."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation changed concurrently. Refresh and try again."); }
    }

    public async Task<Result<TeamFormationDto>> DeclineAsync(
        Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can respond to invitations.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var now = _clock.UtcNow;
                var found = await FindOwnInvitationAsync(formationId, studentId.Value, now, ct);
                if (found.IsFailure) return Failure(found.Error.Code, found.Error.Message);
                var (formation, invitation) = found.Value;
                if (formation.Status != TeamFormationStatus.Pending)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "This invitation is no longer pending.");
                if (invitation.Status == TeamInvitationStatus.Expired)
                    return Failure(ErrorCodes.TeamInvitationExpired, "This invitation has expired.");
                if (invitation.Status != TeamInvitationStatus.Pending || invitation.ReservationReleasedAtUtc.HasValue)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "This invitation is no longer pending.");

                invitation.Status = TeamInvitationStatus.Declined;
                invitation.RespondedAtUtc = now;
                invitation.ReservationReleasedAtUtc = now;
                formation.UpdatedAt = now;
                formation.UpdatedBy = userId;
                ClassOutbox.Enqueue(_context, "TeamFormation.Declined.v1", formation.ClassId, new
                {
                    FormationId = formation.Id,
                    CreatorStudentId = formation.CreatorStudentId,
                    StudentId = studentId.Value
                }, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(await ToDtoAsync(formation, studentId.Value, now, ct));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation changed concurrently. Refresh and try again."); }
    }

    public async Task<Result<TeamFormationDto>> LeaveAsync(
        Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can leave a formation.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var now = _clock.UtcNow;
                var found = await FindOwnInvitationAsync(formationId, studentId.Value, now, ct);
                if (found.IsFailure) return Failure(found.Error.Code, found.Error.Message);
                var (formation, invitation) = found.Value;
                if (formation.CreatorStudentId == studentId.Value)
                    return Failure(ErrorCodes.ClassAccessDenied, "The creator cannot leave; cancel the formation instead.");
                if (formation.Status != TeamFormationStatus.Pending ||
                    invitation.Status != TeamInvitationStatus.Accepted || invitation.ReservationReleasedAtUtc.HasValue)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "Only an accepted member of a pending formation can leave.");

                invitation.Status = TeamInvitationStatus.Left;
                invitation.RespondedAtUtc = now;
                invitation.ReservationReleasedAtUtc = now;
                formation.UpdatedAt = now;
                formation.UpdatedBy = userId;
                ClassOutbox.Enqueue(_context, "TeamFormation.Left.v1", formation.ClassId, new
                {
                    FormationId = formation.Id,
                    CreatorStudentId = formation.CreatorStudentId,
                    StudentId = studentId.Value,
                    WasProposedLeader = formation.ProposedLeaderStudentId == studentId.Value
                }, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(await ToDtoAsync(formation, studentId.Value, now, ct));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation changed concurrently. Refresh and try again."); }
    }

    public async Task<Result<TeamFormationDto>> FinalizeAsync(
        Guid formationId, FinalizeTeamFormationRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await GetCurrentStudentIdAsync(userId, role, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "Only a linked student can finalize a formation.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var now = _clock.UtcNow;
                var formation = await LoadForUpdateAsync(formationId, now, ct);
                if (formation is null) return Failure(ErrorCodes.TeamFormationNotFound, "Formation not found.");
                if (formation.CreatorStudentId != studentId.Value)
                    return Failure(ErrorCodes.ClassAccessDenied, "Only the creator can finalize this formation.");
                if (formation.Status != TeamFormationStatus.Pending)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "Only a pending formation can be finalized.");

                var active = ActiveInvitations(formation).ToArray();
                var accepted = active.Where(item => item.Status == TeamInvitationStatus.Accepted).ToArray();
                var pending = active.Where(item => item.Status == TeamInvitationStatus.Pending).ToArray();
                if (pending.Length > 0 && !request.ConfirmPendingInvitations)
                    return Failure(ErrorCodes.TeamFormationPendingConfirmationRequired,
                        "Some invitations are still waiting for a response. Confirm to close them and finalize the team.");

                var acceptedIds = accepted.Select(item => item.StudentId).ToArray();
                if (acceptedIds.Length < TeamFormationRules.MinMembers || acceptedIds.Length > TeamFormationRules.MaxMembers)
                    return Failure(ErrorCodes.TeamFormationNotReady, string.Join(" ", BuildBlockers(acceptedIds.Length, [])));

                var leaderId = ResolveLeader(formation, acceptedIds, request.LeaderStudentId, out var leaderError);
                if (leaderError is not null) return Failure(ErrorCodes.ClassValidationError, leaderError);

                var members = await ValidateEligibilityAsync(formation.ClassId, acceptedIds, formation.Id, ct);
                if (members.IsFailure) return Failure(members.Error.Code, members.Error.Message);
                var blockers = BuildBlockers(acceptedIds.Length, await ResolveMajorsAsync(members.Value, ct));
                if (blockers.Count > 0) return Failure(ErrorCodes.TeamFormationNotReady, string.Join(" ", blockers));
                var nameError = await ValidateNameAsync(formation.ClassId, formation.TeamName, formation.Id, ct);
                if (nameError is not null) return Failure(nameError.Value.Code, nameError.Value.Message);

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
                        RoleInTeam = member.StudentId == leaderId ? TeamMemberRole.Leader : TeamMemberRole.Member,
                        CountsTowardActiveTeam = true,
                        JoinedAt = now,
                        CreatedById = userId
                    });
                }
                _context.Teams.Add(team);
                formation.ProposedLeaderStudentId = leaderId;
                formation.Status = TeamFormationStatus.Completed;
                formation.CompletedTeamId = team.Id;
                formation.CompletedTeam = team;
                formation.CompletedAtUtc = now;
                formation.UpdatedAt = now;
                formation.UpdatedBy = userId;
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
                    StudentIds = acceptedIds
                }, now);
                if (pending.Length > 0)
                {
                    ClassOutbox.Enqueue(_context, "TeamFormation.Closed.v1", formation.ClassId, new
                    {
                        FormationId = formation.Id,
                        StudentIds = pending.Select(item => item.StudentId).Distinct().ToArray()
                    }, now);
                }
                await _context.SaveChangesAsync(ct);
                return Result.Success(await ToDtoAsync(formation, studentId.Value, now, ct));
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
                var now = _clock.UtcNow;
                var formation = await LoadForUpdateAsync(formationId, now, ct);
                if (formation is null) return Failure(ErrorCodes.TeamFormationNotFound, "Formation not found.");
                if (formation.CreatorStudentId != studentId.Value)
                    return Failure(ErrorCodes.ClassAccessDenied, "Only the creator can cancel this formation.");
                if (formation.Status != TeamFormationStatus.Pending)
                    return Failure(ErrorCodes.TeamFormationStateInvalid, "Only a pending formation can be cancelled.");
                var notified = ActiveInvitations(formation).Select(item => item.StudentId)
                    .Where(id => id != studentId.Value).Distinct().ToArray();
                formation.Status = TeamFormationStatus.Cancelled;
                formation.CancelledAtUtc = now;
                formation.UpdatedAt = now;
                formation.UpdatedBy = userId;
                ReleaseReservations(formation, now);
                ClassOutbox.Enqueue(_context, "TeamFormation.Cancelled.v1", formation.ClassId, new
                {
                    FormationId = formation.Id,
                    StudentIds = notified
                }, now);
                await _context.SaveChangesAsync(ct);
                return Result.Success(await ToDtoAsync(formation, studentId.Value, now, ct));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "Formation changed concurrently. Refresh and try again."); }
    }

    // ----- Shared helpers -----

    /// <summary>Expires overdue pending invitations of the class so reservations are accurate before validating.</summary>
    private async Task SweepAsync(Guid classId, DateTime now, CancellationToken ct)
    {
        var expired = await TeamFormationExpiry.ApplyAsync(_context, now, classId, null, 200, ct);
        if (expired > 0) await _context.SaveChangesAsync(ct);
    }

    private async Task<TeamFormation?> LoadForUpdateAsync(Guid formationId, DateTime now, CancellationToken ct)
    {
        var classId = await _context.TeamFormations.AsNoTracking().Where(item => item.Id == formationId)
            .Select(item => (Guid?)item.ClassId).FirstOrDefaultAsync(ct);
        if (!classId.HasValue) return null;
        await SweepAsync(classId.Value, now, ct);
        return await FormationQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == formationId, ct);
    }

    private async Task<Result<(TeamFormation Formation, TeamFormationInvitation Invitation)>> FindOwnInvitationAsync(
        Guid formationId, Guid studentId, DateTime now, CancellationToken ct)
    {
        var formation = await LoadForUpdateAsync(formationId, now, ct);
        if (formation is null)
            return Result.Failure<(TeamFormation, TeamFormationInvitation)>(
                new Error(ErrorCodes.TeamFormationNotFound, "Formation not found."));
        var invitation = formation.Invitations.Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.CreatedAtUtc).FirstOrDefault();
        if (invitation is null)
            return Result.Failure<(TeamFormation, TeamFormationInvitation)>(
                new Error(ErrorCodes.ClassAccessDenied, "This invitation is not yours."));
        return Result.Success((formation, invitation));
    }

    private static IEnumerable<TeamFormationInvitation> ActiveInvitations(TeamFormation formation) =>
        formation.Invitations.Where(item => item.ReservationReleasedAtUtc == null);

    private static TeamFormationInvitation NewInvitation(
        TeamFormation formation, ClassStudent member, bool isCreator, DateTime now) => new()
    {
        FormationId = formation.Id,
        Formation = formation,
        ClassId = formation.ClassId,
        StudentId = member.StudentId,
        ClassStudent = member,
        Status = isCreator ? TeamInvitationStatus.Accepted : TeamInvitationStatus.Pending,
        CreatedAtUtc = now,
        ExpiresAtUtc = isCreator ? null : now.Add(TeamFormationRules.InvitationLifetime),
        RespondedAtUtc = isCreator ? now : null
    };

    private void EnqueueInvited(TeamFormation formation, IReadOnlyCollection<Guid> inviteeIds, DateTime now) =>
        ClassOutbox.Enqueue(_context, "TeamFormation.Invited.v1", formation.ClassId, new
        {
            FormationId = formation.Id,
            StudentIds = inviteeIds.ToArray(),
            ProposedLeaderStudentId = formation.ProposedLeaderStudentId,
            TeamName = formation.TeamName
        }, now);

    /// <summary>
    /// The proposed leader keeps the role once they accepted. Otherwise the creator must choose an accepted member.
    /// </summary>
    private static Guid ResolveLeader(
        TeamFormation formation, IReadOnlyCollection<Guid> acceptedIds, Guid? requested, out string? error)
    {
        error = null;
        if (acceptedIds.Contains(formation.ProposedLeaderStudentId))
        {
            if (requested.HasValue && requested.Value != formation.ProposedLeaderStudentId)
                error = "The proposed leader accepted the invitation and cannot be replaced.";
            return formation.ProposedLeaderStudentId;
        }
        if (!requested.HasValue || !acceptedIds.Contains(requested.Value))
        {
            error = "Choose a team leader from the accepted members.";
            return Guid.Empty;
        }
        return requested.Value;
    }

    private static List<string> BuildBlockers(int acceptedCount, IReadOnlyCollection<string?> acceptedMajors)
    {
        var blockers = new List<string>();
        if (acceptedCount < TeamFormationRules.MinMembers)
            blockers.Add($"At least {TeamFormationRules.MinMembers} accepted members are required.");
        else if (acceptedCount > TeamFormationRules.MaxMembers)
            blockers.Add($"At most {TeamFormationRules.MaxMembers} members are allowed.");
        else if (!TeamFormationRules.HasGroupOne(acceptedMajors) || !TeamFormationRules.HasGroupTwo(acceptedMajors))
            blockers.Add("Team must include at least one BBA and one BIT student.");
        return blockers;
    }

    /// <summary>Individual eligibility only; team composition is checked at finalize.</summary>
    private async Task<Result<List<ClassStudent>>> ValidateEligibilityAsync(
        Guid classId, IReadOnlyCollection<Guid> ids, Guid? currentFormationId, CancellationToken ct)
    {
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
        var reservationConflict = await _context.TeamFormationInvitations.AsNoTracking()
            .Where(item =>
                item.ClassId == classId && ids.Contains(item.StudentId) && item.ReservationReleasedAtUtc == null &&
                item.Formation.Status == TeamFormationStatus.Pending &&
                (!currentFormationId.HasValue || item.FormationId != currentFormationId.Value))
            .OrderBy(item => item.ClassStudent.Student.RollNumber)
            .Select(item => new
            {
                item.ClassStudent.Student.FullName,
                item.ClassStudent.Student.RollNumber
            })
            .FirstOrDefaultAsync(ct);
        if (reservationConflict is not null)
        {
            var studentLabel = string.IsNullOrWhiteSpace(reservationConflict.RollNumber)
                ? reservationConflict.FullName
                : $"{reservationConflict.FullName} ({reservationConflict.RollNumber})";
            return MemberFailure(
                ErrorCodes.TeamFormationReservationConflict,
                $"{studentLabel} has another pending team invitation.");
        }
        return Result.Success(enrollments);
    }

    private async Task<string?[]> ResolveMajorsAsync(IReadOnlyCollection<ClassStudent> enrollments, CancellationToken ct)
    {
        var registeredMajors = await RegisteredStudentMajorResolver.LoadByEmailAsync(
            _context, enrollments.Select(item => item.Student.Email), ct);
        return enrollments.Select(item =>
        {
            var major = StudentEnrollmentRules.ResolveEffectiveMajorCode(item.MajorCodeAtEnrollment, item.Student.MajorCode);
            if (!MajorCodes.IsValid(major) && !string.IsNullOrWhiteSpace(item.Student.Email) &&
                registeredMajors.TryGetValue(item.Student.Email, out var registeredMajor))
                major = registeredMajor;
            return major?.Trim();
        }).ToArray();
    }

    private async Task<(string Code, string Message)?> ValidateNameAsync(
        Guid classId, string teamName, Guid? currentFormationId, CancellationToken ct)
    {
        var normalized = teamName.Trim().ToLowerInvariant();
        if (normalized.Length is < TeamFormationRules.MinTeamNameLength or > TeamFormationRules.MaxTeamNameLength)
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
        foreach (var invitation in formation.Invitations.Where(item => item.ReservationReleasedAtUtc == null))
            invitation.ReservationReleasedAtUtc = now;
    }

    // ----- DTO mapping -----

    /// <summary>Overdue pending records are reported as Expired even before the background job releases them.</summary>
    private static TeamInvitationStatus EffectiveStatus(TeamFormation formation, TeamFormationInvitation invitation, DateTime now) =>
        formation.Status == TeamFormationStatus.Pending && invitation.Status == TeamInvitationStatus.Pending &&
        invitation.ReservationReleasedAtUtc == null && invitation.ExpiresAtUtc.HasValue && invitation.ExpiresAtUtc.Value <= now
            ? TeamInvitationStatus.Expired
            : invitation.Status;

    private async Task<IReadOnlyCollection<TeamFormationDto>> ToDtosAsync(
        IEnumerable<TeamFormation> formations, Guid myStudentId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var result = new List<TeamFormationDto>();
        foreach (var formation in formations) result.Add(await ToDtoAsync(formation, myStudentId, now, ct));
        return result;
    }

    private async Task<TeamFormationDto> ToDtoAsync(
        TeamFormation formation, Guid myStudentId, DateTime now, CancellationToken ct)
    {
        var isPending = formation.Status == TeamFormationStatus.Pending;
        var active = isPending
            ? formation.Invitations.Where(item => item.ReservationReleasedAtUtc == null &&
                EffectiveStatus(formation, item, now) is TeamInvitationStatus.Pending or TeamInvitationStatus.Accepted).ToArray()
            : [];
        var accepted = active.Where(item => item.Status == TeamInvitationStatus.Accepted).ToArray();
        var blockers = new List<string>();
        if (isPending)
        {
            string?[] majors = [];
            if (accepted.Length is >= TeamFormationRules.MinMembers and <= TeamFormationRules.MaxMembers)
                majors = await ResolveMajorsAsync(accepted.Select(item => item.ClassStudent).ToArray(), ct);
            blockers = BuildBlockers(accepted.Length, majors);
        }
        var latestByStudent = formation.Invitations.GroupBy(item => item.StudentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAtUtc).First().Id);

        return new TeamFormationDto
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
            ServerTimeUtc = now,
            AcceptedCount = accepted.Length,
            PendingCount = active.Length - accepted.Length,
            ActiveCount = active.Length,
            CanFinalize = isPending && blockers.Count == 0,
            FinalizeBlockers = blockers,
            RequiresLeaderSelection = isPending &&
                accepted.All(item => item.StudentId != formation.ProposedLeaderStudentId),
            Invitations = formation.Invitations
                .OrderBy(item => item.ClassStudent.Student.RollNumber).ThenBy(item => item.CreatedAtUtc)
                .Select(item => new TeamFormationInvitationDto
                {
                    Id = item.Id,
                    StudentId = item.StudentId,
                    FullName = item.ClassStudent.Student.FullName,
                    RollNumber = item.ClassStudent.Student.RollNumber ?? string.Empty,
                    Status = EffectiveStatus(formation, item, now).ToString(),
                    IsCreator = item.StudentId == formation.CreatorStudentId,
                    IsProposedLeader = item.StudentId == formation.ProposedLeaderStudentId,
                    IsCurrent = latestByStudent[item.StudentId] == item.Id,
                    CreatedAtUtc = item.CreatedAtUtc,
                    ExpiresAtUtc = item.ExpiresAtUtc,
                    RespondedAtUtc = item.RespondedAtUtc
                }).ToArray()
        };
    }

    private static Result<List<ClassStudent>> MemberFailure(string code, string message) =>
        Result.Failure<List<ClassStudent>>(new Error(code, message));
    private static Result<TeamFormationDto> Failure(string code, string message) =>
        Result.Failure<TeamFormationDto>(new Error(code, message));
    private static Result<IReadOnlyCollection<TeamFormationDto>> FailureList(string code, string message) =>
        Result.Failure<IReadOnlyCollection<TeamFormationDto>>(new Error(code, message));
}
