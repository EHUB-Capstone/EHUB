using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Mentoring;

public sealed class MentoringSessionHandler(IApplicationDbContext context, IDateTimeProvider clock) : IMentoringSessionHandler
{
    public async Task<Result<IReadOnlyCollection<MentoringSessionResponse>>> ListAsync(Guid? teamId, Guid userId, string role, CancellationToken ct)
    {
        var query = context.MentoringSessions.AsNoTracking().Include(x => x.ActionItems)
            .Include(x => x.MentorAssignment).ThenInclude(x => x.Team).ThenInclude(x => x.Class)
            .Include(x => x.MentorAssignment).ThenInclude(x => x.MentorProfile).AsQueryable();
        if (teamId.HasValue) query = query.Where(x => x.MentorAssignment.TeamId == teamId.Value);
        query = role switch
        {
            SystemRoles.Admin => query,
            SystemRoles.Lecturer => query.Where(x => x.MentorAssignment.Team.Class.PrimaryLecturerId == userId),
            SystemRoles.Mentor => query.Where(x => x.MentorAssignment.MentorProfile.UserId == userId),
            SystemRoles.Student => query.Where(x => x.MentorAssignment.Team.TeamMembers.Any(m =>
                m.CountsTowardActiveTeam && m.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
                m.ClassStudent.Student.UserId == userId)),
            _ => query.Where(_ => false)
        };
        var sessions = await query.OrderByDescending(x => x.StartAt).Take(200).ToListAsync(ct);
        return Result.Success<IReadOnlyCollection<MentoringSessionResponse>>(sessions.Select(Map).ToArray());
    }

    public async Task<Result<MentoringSessionResponse>> CreateAsync(SaveMentoringSessionRequest request, Guid userId, string role, CancellationToken ct)
    {
        var invalid = Validate(request);
        if (invalid is not null) return Bad<MentoringSessionResponse>(invalid);
        var candidates = await context.MentorAssignments.Include(x => x.Team).ThenInclude(x => x.Class)
            .Include(x => x.MentorProfile).Where(x => x.TeamId == request.TeamId &&
                x.Status == MentorAssignmentStatus.Active && x.EndedAt == null &&
                (!request.MentorAssignmentId.HasValue || x.Id == request.MentorAssignmentId.Value) &&
                (role != SystemRoles.Mentor || x.MentorProfile.UserId == userId)).ToListAsync(ct);
        if (candidates.Count > 1) return Bad<MentoringSessionResponse>("Choose the mentor assignment for this session.");
        var assignment = candidates.SingleOrDefault();
        if (assignment is null && role == SystemRoles.Mentor) return Forbidden<MentoringSessionResponse>();
        if (assignment is null) return Result.Failure<MentoringSessionResponse>(ErrorCodes.MentorNotAvailable, "Team has no active mentor.");
        if (!CanManage(assignment, userId, role)) return Forbidden<MentoringSessionResponse>();
        if (assignment.Team.Status != TeamStatus.Active || assignment.Team.Class.Status != ClassStatus.Active)
            return Bad<MentoringSessionResponse>("Mentoring is available only for an active team and class.");
        if (request.StartAt <= new DateTimeOffset(clock.UtcNow)) return Bad<MentoringSessionResponse>("Session must start in the future.");
        if (await OverlapsAsync(assignment.MentorProfileId, request.StartAt, request.EndAt, null, ct))
            return Bad<MentoringSessionResponse>("The mentor already has a session during this time.");
        var session = new MentoringSession { MentorAssignmentId = assignment.Id,
            LecturerUserId = role == SystemRoles.Lecturer ? userId : null, Title = request.Title.Trim(),
            Description = request.Description?.Trim(), StartAt = request.StartAt.ToUniversalTime(),
            EndAt = request.EndAt.ToUniversalTime(), Location = request.Location?.Trim(),
            MeetingUrl = request.MeetingUrl?.Trim(), Status = MentoringSessionStatus.Scheduled, CreatedBy = userId };
        context.MentoringSessions.Add(session);
        await context.SaveChangesAsync(ct);
        session.MentorAssignment = assignment;
        return Result.Success(Map(session));
    }

    public async Task<Result<MentoringSessionResponse>> UpdateAsync(Guid id, SaveMentoringSessionRequest request, Guid userId, string role, CancellationToken ct)
    {
        var invalid = Validate(request);
        if (invalid is not null) return Bad<MentoringSessionResponse>(invalid);
        var session = await LoadAsync(id, ct);
        if (session is null) return NotFound<MentoringSessionResponse>();
        if (!CanManage(session.MentorAssignment, userId, role)) return Forbidden<MentoringSessionResponse>();
        if ((request.MentorAssignmentId.HasValue && request.MentorAssignmentId != session.MentorAssignmentId) || session.MentorAssignment.TeamId != request.TeamId || session.Status != MentoringSessionStatus.Scheduled)
            return Bad<MentoringSessionResponse>("Only a scheduled session for the same team can be edited.");
        if (session.MentorAssignment.Team.Class.Status != ClassStatus.Active)
            return Bad<MentoringSessionResponse>("Archived or completed classes cannot change sessions.");
        if (request.StartAt <= new DateTimeOffset(clock.UtcNow)) return Bad<MentoringSessionResponse>("Session must start in the future.");
        if (await OverlapsAsync(session.MentorAssignment.MentorProfileId, request.StartAt, request.EndAt, id, ct))
            return Bad<MentoringSessionResponse>("The mentor already has a session during this time.");
        session.Title = request.Title.Trim(); session.Description = request.Description?.Trim();
        session.StartAt = request.StartAt.ToUniversalTime(); session.EndAt = request.EndAt.ToUniversalTime();
        session.Location = request.Location?.Trim(); session.MeetingUrl = request.MeetingUrl?.Trim();
        await context.SaveChangesAsync(ct);
        return Result.Success(Map(session));
    }

    public async Task<Result<MentoringSessionResponse>> CompleteAsync(Guid id, SaveMentoringNotesRequest request, Guid userId, string role, CancellationToken ct)
    {
        var session = await LoadAsync(id, ct);
        if (session is null) return NotFound<MentoringSessionResponse>();
        if (!CanManage(session.MentorAssignment, userId, role)) return Forbidden<MentoringSessionResponse>();
        if (session.MentorAssignment.Team.Class.Status != ClassStatus.Active)
            return Bad<MentoringSessionResponse>("Archived or completed classes cannot change sessions.");
        if (session.Status != MentoringSessionStatus.Scheduled || string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Length > 10000)
            return Bad<MentoringSessionResponse>("A scheduled session requires notes of at most 10000 characters.");
        if (session.EndAt > new DateTimeOffset(clock.UtcNow))
            return Bad<MentoringSessionResponse>("A session can be completed only after it ends.");
        session.Notes = request.Notes.Trim(); session.Status = MentoringSessionStatus.Completed;
        await context.SaveChangesAsync(ct);
        return Result.Success(Map(session));
    }

    public async Task<Result<MentoringSessionResponse>> CancelAsync(Guid id, Guid userId, string role, CancellationToken ct)
    {
        var session = await LoadAsync(id, ct);
        if (session is null) return NotFound<MentoringSessionResponse>();
        if (!CanManage(session.MentorAssignment, userId, role)) return Forbidden<MentoringSessionResponse>();
        if (session.MentorAssignment.Team.Class.Status != ClassStatus.Active)
            return Bad<MentoringSessionResponse>("Archived or completed classes cannot change sessions.");
        if (session.Status != MentoringSessionStatus.Scheduled) return Bad<MentoringSessionResponse>("Only scheduled sessions can be cancelled.");
        session.Status = MentoringSessionStatus.Cancelled;
        await context.SaveChangesAsync(ct);
        return Result.Success(Map(session));
    }

    public async Task<Result<MentoringActionItemResponse>> AddActionItemAsync(Guid id, CreateMentoringActionItemRequest request, Guid userId, string role, CancellationToken ct)
    {
        var session = await LoadAsync(id, ct);
        if (session is null) return NotFound<MentoringActionItemResponse>();
        if (!CanManage(session.MentorAssignment, userId, role)) return Forbidden<MentoringActionItemResponse>();
        if (session.MentorAssignment.Team.Class.Status != ClassStatus.Active)
            return Bad<MentoringActionItemResponse>("Archived or completed classes cannot change sessions.");
        if (session.Status != MentoringSessionStatus.Completed || string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
            return Bad<MentoringActionItemResponse>("A completed session requires action content of at most 2000 characters.");
        var item = new MentoringActionItem { MentoringSessionId = id, Content = request.Content.Trim(),
            DueDate = request.DueDate?.ToUniversalTime(), CreatedBy = userId };
        context.MentoringActionItems.Add(item);
        await context.SaveChangesAsync(ct);
        return Result.Success(MapItem(item));
    }

    public async Task<Result<MentoringFeedbackResponse>> SaveFeedbackAsync(Guid id, SaveMentoringFeedbackRequest request,
        Guid userId, string role, CancellationToken ct)
    {
        if (role != SystemRoles.Student) return Forbidden<MentoringFeedbackResponse>();
        if (request.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Length > 2000)
            return Bad<MentoringFeedbackResponse>("Rating must be 1–5 and a comment is required (maximum 2000 characters).");
        var session = await LoadAsync(id, ct);
        if (session is null) return NotFound<MentoringFeedbackResponse>();
        var member = await context.TeamMembers.AsNoTracking().AnyAsync(x =>
            x.TeamId == session.MentorAssignment.TeamId && x.CountsTowardActiveTeam &&
            x.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && x.ClassStudent.Student.UserId == userId, ct);
        if (!member) return Forbidden<MentoringFeedbackResponse>();
        if (session.MentorAssignment.Team.Class.Status == ClassStatus.Archived)
            return Bad<MentoringFeedbackResponse>("Archived classes cannot receive feedback.");
        if (session.Status != MentoringSessionStatus.Completed)
            return Bad<MentoringFeedbackResponse>("Feedback can be submitted after a session is completed.");
        var existing = await context.MentoringFeedback.FirstOrDefaultAsync(x => x.MentoringSessionId == id && x.StudentUserId == userId, ct);
        if (existing is null)
        {
            existing = new MentoringFeedback { MentoringSessionId = id, StudentUserId = userId, CreatedBy = userId };
            context.MentoringFeedback.Add(existing);
        }
        existing.Rating = request.Rating; existing.Comment = request.Comment.Trim();
        await context.SaveChangesAsync(ct);
        return Result.Success(MapFeedback(existing));
    }

    public async Task<Result<IReadOnlyCollection<MentoringFeedbackResponse>>> GetFeedbackAsync(Guid id, Guid userId,
        string role, CancellationToken ct)
    {
        var session = await LoadAsync(id, ct);
        if (session is null) return NotFound<IReadOnlyCollection<MentoringFeedbackResponse>>();
        var canView = CanManage(session.MentorAssignment, userId, role);
        if (!canView) return Forbidden<IReadOnlyCollection<MentoringFeedbackResponse>>();
        var feedback = await context.MentoringFeedback.AsNoTracking()
            .Where(x => x.MentoringSessionId == id).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Result.Success<IReadOnlyCollection<MentoringFeedbackResponse>>(feedback.Select(MapFeedback).ToArray());
    }

    private async Task<MentoringSession?> LoadAsync(Guid id, CancellationToken ct) => await context.MentoringSessions
        .Include(x => x.ActionItems).Include(x => x.MentorAssignment).ThenInclude(x => x.Team).ThenInclude(x => x.Class)
        .Include(x => x.MentorAssignment).ThenInclude(x => x.MentorProfile)
        .FirstOrDefaultAsync(x => x.Id == id, ct);
    private Task<bool> OverlapsAsync(Guid mentorProfileId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId,
        CancellationToken ct) => context.MentoringSessions.AsNoTracking().AnyAsync(x =>
            x.MentorAssignment.MentorProfileId == mentorProfileId && x.Status == MentoringSessionStatus.Scheduled &&
            (!excludeId.HasValue || x.Id != excludeId.Value) && x.StartAt < end && start < x.EndAt, ct);
    private static bool CanManage(MentorAssignment x, Guid userId, string role) => role == SystemRoles.Admin ||
        (role == SystemRoles.Lecturer && x.Team.Class.PrimaryLecturerId == userId) ||
        (role == SystemRoles.Mentor && x.MentorProfile.UserId == userId);
    private static string? Validate(SaveMentoringSessionRequest x) =>
        x.TeamId == Guid.Empty || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 200 ||
        (x.Description?.Length ?? 0) > 2000 || x.EndAt <= x.StartAt ||
        (x.Location?.Length ?? 0) > 300 || (x.MeetingUrl?.Length ?? 0) > 1000 ||
        (!string.IsNullOrWhiteSpace(x.MeetingUrl) &&
            (!Uri.TryCreate(x.MeetingUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps))
            ? "Session fields are invalid." : null;
    private static MentoringSessionResponse Map(MentoringSession x) => new() { Id = x.Id,
        TeamId = x.MentorAssignment.TeamId, MentorAssignmentId = x.MentorAssignmentId,
        Title = x.Title, Description = x.Description, StartAt = x.StartAt, EndAt = x.EndAt,
        Location = x.Location, MeetingUrl = x.MeetingUrl, Status = x.Status.ToString(), Notes = x.Notes,
        ActionItems = x.ActionItems.Select(MapItem).ToArray() };
    private static MentoringActionItemResponse MapItem(MentoringActionItem x) => new() { Id = x.Id,
        Content = x.Content, DueDate = x.DueDate, Completed = x.Completed };
    private static MentoringFeedbackResponse MapFeedback(MentoringFeedback x) => new() { Id = x.Id,
        Rating = x.Rating, Comment = x.Comment, CreatedAtUtc = x.CreatedAt };
    private static Result<T> Bad<T>(string message) => Result.Failure<T>(ErrorCodes.CommonValidationError, message);
    private static Result<T> Forbidden<T>() => Result.Failure<T>(ErrorCodes.ClassAccessDenied, "Access to this mentoring session is denied.");
    private static Result<T> NotFound<T>() => Result.Failure<T>(ErrorCodes.CommonNotFoundError, "Mentoring session was not found.");
}
