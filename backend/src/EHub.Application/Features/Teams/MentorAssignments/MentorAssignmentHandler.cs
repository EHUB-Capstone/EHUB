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
            .Where(profile => profile.Status == MentorProfileStatus.Active && profile.User.Status == UserStatus.Active)
            .OrderBy(profile => profile.User.FullName)
            .Select(profile => new MentorCandidateDto
            {
                Mentor = new MentorSummaryDto
                {
                    MentorProfileId = profile.Id,
                    UserId = profile.UserId,
                    FullName = profile.User.FullName,
                    Email = profile.User.Email,
                    Organization = profile.Organization
                },
                ActiveTeamCount = profile.Assignments.Count(assignment => assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null),
                MaxTeams = profile.MaxTeams,
                HasCapacity = profile.Assignments.Count(assignment => assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null) < profile.MaxTeams
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

                var current = await _context.MentorAssignments
                    .Include(item => item.Team).Include(item => item.MentorProfile).ThenInclude(profile => profile.User)
                    .Where(item => item.TeamId == teamId && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
                    .ToListAsync(transactionCancellationToken);
                var same = current.FirstOrDefault(item => item.MentorProfileId == mentor.Id);
                if (same != null) return Result.Success(TeamMappings.ToMentorAssignmentDto(same));

                var activeTeamCount = await _context.MentorAssignments.AsNoTracking()
                    .CountAsync(item => item.MentorProfileId == mentor.Id && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null, transactionCancellationToken);
                if (activeTeamCount >= mentor.MaxTeams)
                    return Failure(ErrorCodes.MentorCapacityReached, "The selected mentor has reached the maximum active team capacity.");

                var now = DateTime.UtcNow;
                foreach (var existing in current)
                {
                    existing.Status = MentorAssignmentStatus.Ended;
                    existing.EndedAt = now;
                }

                var assignment = new MentorAssignment
                {
                    MentorProfileId = mentor.Id,
                    MentorProfile = mentor,
                    TeamId = team.Id,
                    Team = team,
                    AssignedById = userId,
                    AssignedAt = now,
                    Status = MentorAssignmentStatus.Active,
                    Note = request.Note?.Trim(),
                    CreatedBy = userId
                };
                _context.MentorAssignments.Add(assignment);
                _context.ClassAuditLogs.Add(new ClassAuditLog
                {
                    ClassId = team.ClassId,
                    Action = current.Count == 0 ? "MENTOR_ASSIGNED" : "MENTOR_REASSIGNED",
                    PerformedByUserId = userId,
                    OccurredAtUtc = now,
                    DetailsJson = JsonSerializer.Serialize(new { TeamId = team.Id, MentorProfileId = mentor.Id })
                });
                ClassOutbox.Enqueue(_context, "Team.MentorAssignmentChanged.v1", team.ClassId, new
                {
                    TeamId = team.Id,
                    MentorProfileId = mentor.Id,
                    MentorUserId = mentor.UserId,
                    Action = current.Count == 0 ? "Assigned" : "Reassigned"
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

        var current = await _context.MentorAssignments.Include(item => item.Team).ThenInclude(item => item.Class)
            .FirstOrDefaultAsync(item => item.TeamId == teamId && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null, cancellationToken);
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
            DetailsJson = JsonSerializer.Serialize(new { TeamId = teamId, current.MentorProfileId, Reason = request.Reason.Trim() })
        });
        ClassOutbox.Enqueue(_context, "Team.MentorAssignmentChanged.v1", current.Team.ClassId, new { TeamId = teamId, Action = "Ended" }, now);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<MentorAssignment> AssignmentQuery() => _context.MentorAssignments.AsNoTracking()
        .Include(item => item.Team)
        .Include(item => item.MentorProfile).ThenInclude(profile => profile.User);

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<MentorAssignmentDto> Failure(string code, string message) => Result.Failure<MentorAssignmentDto>(new Error(code, message));
    private static Result<IReadOnlyCollection<MentorAssignmentDto>> FailureList(string code, string message) => Result.Failure<IReadOnlyCollection<MentorAssignmentDto>>(new Error(code, message));
}
