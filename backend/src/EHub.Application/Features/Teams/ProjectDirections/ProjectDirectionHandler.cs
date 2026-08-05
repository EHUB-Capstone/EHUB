using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.ProjectDirections;

public sealed class ProjectDirectionHandler : IProjectDirectionHandler
{
    private readonly IApplicationDbContext _context;

    public ProjectDirectionHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProjectDirectionDto>> GetAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var team = await TeamAccessQuery().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
        if (!await CanViewAsync(team, userId, role, cancellationToken)) return Failure(ErrorCodes.ClassAccessDenied, "You cannot view this project direction.");
        var direction = await DirectionQuery().FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (direction == null) return Failure(ErrorCodes.ProjectDirectionNotFound, "The team has not created a project direction.");
        return Result.Success(ToDto(direction));
    }

    public async Task<Result<ProjectDirectionDto>> SaveAsync(
        Guid teamId, SaveProjectDirectionRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var validation = ValidateContent(request.Title, request.Summary);
        if (validation != null) return Failure(validation.Value.Code, validation.Value.Message);
        var team = await TeamAccessQuery().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
        if (!await IsLeaderAsync(team, userId, role, cancellationToken)) return Failure(ErrorCodes.ClassAccessDenied, "Only the team leader can edit the project direction.");
        if (team.Status != TeamStatus.Active || team.Class.Status == ClassStatus.Archived) return Failure(ErrorCodes.TeamInactive, "Project direction cannot be changed for this team.");

        var direction = await DirectionQuery(tracking: true).FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (direction == null)
        {
            if (!string.IsNullOrWhiteSpace(request.RowVersion)) return Failure(ErrorCodes.ClassConcurrencyConflict, "The project direction no longer matches this screen.");
            direction = new ProjectDirection
            {
                TeamId = teamId,
                Team = team,
                Title = request.Title.Trim(),
                Summary = request.Summary.Trim(),
                Status = ProjectDirectionStatus.Draft,
                CreatedBy = userId
            };
            _context.ProjectDirections.Add(direction);
        }
        else
        {
            if (!uint.TryParse(request.RowVersion, out var version) || direction.Version != version)
                return Failure(ErrorCodes.ClassConcurrencyConflict, "The project direction changed concurrently. Refresh and try again.");
            if (direction.Status is not (ProjectDirectionStatus.Draft or ProjectDirectionStatus.NeedsRevision))
                return Failure(ErrorCodes.ProjectDirectionStateInvalid, "Only Draft or NeedsRevision directions can be edited.");
            direction.Title = request.Title.Trim();
            direction.Summary = request.Summary.Trim();
            direction.UpdatedBy = userId;
        }

        ClassOutbox.Enqueue(_context, "ProjectDirection.Saved.v1", team.ClassId, new { TeamId = teamId, ProjectDirectionId = direction.Id });
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The project direction changed concurrently. Refresh and try again."); }
        catch (DbUpdateException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "A project direction was created concurrently. Refresh and try again."); }
        return Result.Success(ToDto(direction));
    }

    public async Task<Result<ProjectDirectionDto>> SubmitAsync(
        Guid teamId, ProjectDirectionStateRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        var team = await TeamAccessQuery().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
        if (!await IsLeaderAsync(team, userId, role, cancellationToken)) return Failure(ErrorCodes.ClassAccessDenied, "Only the team leader can submit the project direction.");
        if (team.Status != TeamStatus.Active || team.Class.Status == ClassStatus.Archived)
            return Failure(ErrorCodes.TeamInactive, "Project direction cannot be submitted for an inactive team or archived class.");
        var direction = await DirectionQuery(tracking: true).FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (direction == null) return Failure(ErrorCodes.ProjectDirectionNotFound, "Create the project direction before submitting it.");
        if (direction.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The project direction changed concurrently. Refresh and try again.");
        if (direction.Status is not (ProjectDirectionStatus.Draft or ProjectDirectionStatus.NeedsRevision))
            return Failure(ErrorCodes.ProjectDirectionStateInvalid, "Only Draft or NeedsRevision directions can be submitted.");
        var now = DateTime.UtcNow;
        direction.Status = ProjectDirectionStatus.Submitted;
        direction.SubmittedAtUtc = now;
        direction.UpdatedBy = userId;
        ClassOutbox.Enqueue(_context, "ProjectDirection.Submitted.v1", team.ClassId, new
        {
            TeamId = teamId,
            ProjectDirectionId = direction.Id,
            LecturerUserId = team.Class.PrimaryLecturerId
        }, now);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The project direction changed concurrently. Refresh and try again."); }
        return Result.Success(ToDto(direction));
    }

    public async Task<Result<ProjectDirectionDto>> ReviewAsync(
        Guid teamId, ReviewProjectDirectionRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Lecturer)) return Failure(ErrorCodes.ClassAccessDenied, "Only the assigned lecturer can review a project direction.");
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Trim().Length is < 3 or > 1_000)
            return Failure(ErrorCodes.ClassValidationError, "Review comment must be between 3 and 1000 characters.");
        if (!Enum.TryParse<ProjectDirectionStatus>(request.Decision, true, out var decision) || decision is not (ProjectDirectionStatus.Approved or ProjectDirectionStatus.NeedsRevision))
            return Failure(ErrorCodes.ClassValidationError, "Decision must be Approved or NeedsRevision.");
        var team = await TeamAccessQuery().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
        if (team.Class.PrimaryLecturerId != userId) return Failure(ErrorCodes.ClassAccessDenied, "A lecturer cannot review project directions outside assigned classes.");
        if (team.Status != TeamStatus.Active || team.Class.Status == ClassStatus.Archived)
            return Failure(ErrorCodes.TeamInactive, "Project direction cannot be reviewed for an inactive team or archived class.");
        var direction = await DirectionQuery(tracking: true).FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (direction == null) return Failure(ErrorCodes.ProjectDirectionNotFound, "The project direction was not found.");
        if (direction.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The project direction changed concurrently. Refresh and try again.");
        if (direction.Status != ProjectDirectionStatus.Submitted) return Failure(ErrorCodes.ProjectDirectionStateInvalid, "Only a Submitted direction can be reviewed.");

        var now = DateTime.UtcNow;
        var previous = direction.Status;
        direction.Status = decision;
        direction.ReviewedAtUtc = now;
        direction.ReviewedByUserId = userId;
        direction.UpdatedBy = userId;
        direction.Reviews.Add(new ProjectDirectionReview
        {
            ProjectDirectionId = direction.Id,
            ProjectDirection = direction,
            FromStatus = previous,
            ToStatus = decision,
            Comment = request.Comment.Trim(),
            ReviewedByUserId = userId,
            OccurredAtUtc = now
        });
        ClassOutbox.Enqueue(_context, "ProjectDirection.Reviewed.v1", team.ClassId, new
        {
            TeamId = teamId,
            ProjectDirectionId = direction.Id,
            Decision = decision.ToString(),
            StudentUserIds = team.TeamMembers.Where(member => member.CountsTowardActiveTeam && member.ClassStudent.Student.UserId.HasValue)
                .Select(member => member.ClassStudent.Student.UserId!.Value).ToArray()
        }, now);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The project direction was reviewed concurrently. Refresh the page."); }
        return Result.Success(ToDto(direction));
    }

    private IQueryable<Team> TeamAccessQuery() => _context.Teams
        .Include(team => team.Class)
        .Include(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
        .Include(team => team.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile);

    private IQueryable<ProjectDirection> DirectionQuery(bool tracking = false)
    {
        var query = tracking ? _context.ProjectDirections.AsQueryable() : _context.ProjectDirections.AsNoTracking();
        return query.Include(direction => direction.Reviews);
    }

    private async Task<bool> CanViewAsync(Team team, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (IsRole(role, SystemRoles.Admin)) return true;
        if (IsRole(role, SystemRoles.Lecturer)) return team.Class.PrimaryLecturerId == userId;
        if (IsRole(role, SystemRoles.Mentor)) return team.MentorAssignments.Any(item => item.MentorProfile.UserId == userId && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null);
        return await IsLeaderOrMemberAsync(team, userId, role, cancellationToken);
    }

    private async Task<bool> IsLeaderAsync(Team team, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (!IsRole(role, SystemRoles.Student)) return false;
        var studentId = await _context.Students.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        return studentId.HasValue && team.TeamMembers.Any(item => item.StudentId == studentId && item.CountsTowardActiveTeam && item.RoleInTeam == TeamMemberRole.Leader);
    }

    private async Task<bool> IsLeaderOrMemberAsync(Team team, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (!IsRole(role, SystemRoles.Student)) return false;
        var studentId = await _context.Students.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        return studentId.HasValue && team.TeamMembers.Any(item => item.StudentId == studentId && item.CountsTowardActiveTeam);
    }

    private static (string Code, string Message)? ValidateContent(string title, string summary)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < 3 or > 200) return (ErrorCodes.ClassValidationError, "Direction title must be between 3 and 200 characters.");
        if (string.IsNullOrWhiteSpace(summary) || summary.Trim().Length is < 20 or > 5_000) return (ErrorCodes.ClassValidationError, "Direction summary must be between 20 and 5000 characters.");
        return null;
    }

    private static ProjectDirectionDto ToDto(ProjectDirection direction) => new()
    {
        Id = direction.Id, TeamId = direction.TeamId, Title = direction.Title, Summary = direction.Summary,
        Status = direction.Status.ToString(), SubmittedAtUtc = direction.SubmittedAtUtc, ReviewedAtUtc = direction.ReviewedAtUtc,
        RowVersion = direction.Version.ToString(),
        Reviews = direction.Reviews.OrderByDescending(review => review.OccurredAtUtc).Select(review => new ProjectDirectionReviewDto
        {
            Id = review.Id, FromStatus = review.FromStatus.ToString(), ToStatus = review.ToStatus.ToString(),
            Comment = review.Comment, ReviewedByUserId = review.ReviewedByUserId, OccurredAtUtc = review.OccurredAtUtc
        }).ToArray()
    };

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<ProjectDirectionDto> Failure(string code, string message) => Result.Failure<ProjectDirectionDto>(new Error(code, message));
}
