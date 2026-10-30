using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointFeedback;

public sealed class CheckpointFeedbackHandler(IApplicationDbContext context, ICheckpointFeedbackRealtimePublisher realtimePublisher) : ICheckpointFeedbackHandler
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public async Task<Result<WorkspaceCheckpointFeedbackResponse>> CreateAsync(Guid teamId, int checkpointNumber, CreateWorkspaceCheckpointFeedbackRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var content = request.Comment?.Trim() ?? string.Empty;
        if (content.Length is < 1 or > 20_000) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceValidationError, "A comment must contain between 1 and 20,000 characters.");
        var team = await context.Teams.Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null || !CanAccess(team, userId, role) ||
            (Is(role, SystemRoles.Student) && !await ProjectWorkspaceApprovalRules.IsApprovedAsync(context, teamId, cancellationToken))) return Denied();
        var checkpoint = await context.Checkpoints.FirstOrDefaultAsync(item => item.CourseId == team.Class.CourseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber && item.Status != CheckpointStatus.Archived, cancellationToken);
        var project = await context.Projects.FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (checkpoint is null || project is null) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceNotFound, "The checkpoint workspace was not found.");
        var submission = await context.Submissions.OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt).FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == checkpoint.Id, cancellationToken);
        var now = DateTime.UtcNow;
        if (submission is null)
        {
            submission = new Submission { ProjectId = project.Id, TeamId = teamId, CheckpointId = checkpoint.Id, Title = checkpoint.Name, Status = SubmissionStatus.Draft, VersionNumber = 1, CreatedAt = now, CreatedBy = userId };
            context.Submissions.Add(submission);
        }
        if (request.ParentFeedbackId.HasValue && !await context.SubmissionFeedbacks.AnyAsync(item =>
                item.Id == request.ParentFeedbackId && item.Submission.TeamId == teamId &&
                item.Submission.CheckpointId == checkpoint.Id, cancellationToken))
            return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceValidationError, "The parent comment does not belong to this checkpoint.");
        var feedback = new SubmissionFeedback { Submission = submission, Content = content, CreatedById = userId, ParentFeedbackId = request.ParentFeedbackId, CreatedAt = now, CreatedBy = userId };
        context.SubmissionFeedbacks.Add(feedback);
        await context.SaveChangesAsync(cancellationToken);
        var user = await context.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role).AsNoTracking().SingleAsync(item => item.Id == userId, cancellationToken);
        var response = new WorkspaceCheckpointFeedbackResponse { Id = feedback.Id, CheckpointNumber = checkpointNumber, Comment = feedback.Content, ParentFeedbackId = feedback.ParentFeedbackId, CreatedAt = now, User = new WorkspaceCheckpointUserResponse { Id = user.Id, Name = user.FullName, Role = ResolveRole(user), AvatarUrl = user.AvatarUrl } };
        var recipients = await RecipientUserIdsAsync(team, cancellationToken);
        await realtimePublisher.PublishAsync(recipients, teamId, response, cancellationToken);
        return Result.Success(response);
    }

    public async Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid feedbackId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var team = await context.Teams.Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null || !CanAccess(team, userId, role) ||
            (Is(role, SystemRoles.Student) && !await ProjectWorkspaceApprovalRules.IsApprovedAsync(context, teamId, cancellationToken))) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var feedback = await context.SubmissionFeedbacks.Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == feedbackId && item.Submission.TeamId == teamId && item.Submission.Checkpoint.CheckpointNumber == checkpointNumber, cancellationToken);
        if (feedback is null) return Result.Failure(ErrorCodes.CommonNotFoundError, "Feedback was not found.");
        if (!Is(role, SystemRoles.Admin) && feedback.CreatedById != userId) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only delete your own feedback.");
        var now = DateTime.UtcNow;
        feedback.IsDeleted = true; feedback.DeletedAt = now; feedback.DeletedBy = userId;
        var replies = await context.SubmissionFeedbacks.Where(item => item.ParentFeedbackId == feedbackId).ToListAsync(cancellationToken);
        foreach (var reply in replies) { reply.ParentFeedbackId = null; reply.UpdatedAt = now; reply.UpdatedBy = userId; }
        await context.SaveChangesAsync(cancellationToken);
        await realtimePublisher.PublishDeletedAsync(await RecipientUserIdsAsync(team, cancellationToken), teamId, checkpointNumber, feedbackId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<WorkspaceCheckpointFeedbackResponse>> UpdateAsync(Guid teamId, int checkpointNumber, Guid feedbackId, UpdateWorkspaceCheckpointFeedbackRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var content = request.Comment?.Trim() ?? string.Empty;
        if (content.Length is < 1 or > 20_000) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceValidationError, "A comment must contain between 1 and 20,000 characters.");

        var team = await context.Teams.Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null || !CanAccess(team, userId, role) ||
            (Is(role, SystemRoles.Student) && !await ProjectWorkspaceApprovalRules.IsApprovedAsync(context, teamId, cancellationToken))) return Denied();

        var feedback = await context.SubmissionFeedbacks.Include(item => item.Creator).Include(item => item.Submission).ThenInclude(item => item.Checkpoint)
            .FirstOrDefaultAsync(item => item.Id == feedbackId && item.Submission.TeamId == teamId && item.Submission.Checkpoint.CheckpointNumber == checkpointNumber, cancellationToken);
        if (feedback is null) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.CommonNotFoundError, "Feedback was not found.");
        if (!Is(role, SystemRoles.Mentor) && !Is(role, SystemRoles.Lecturer) && !Is(role, SystemRoles.Student)) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceAccessDenied, "Only mentors, lecturers, and students can edit feedback.");
        if (feedback.CreatedById != userId) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceAccessDenied, "You can only edit your own feedback.");

        feedback.Content = content;
        feedback.UpdatedAt = DateTime.UtcNow;
        feedback.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken);

        var response = new WorkspaceCheckpointFeedbackResponse
        {
            Id = feedback.Id,
            CheckpointNumber = checkpointNumber,
            Comment = feedback.Content,
            ParentFeedbackId = feedback.ParentFeedbackId,
            CreatedAt = feedback.CreatedAt,
            User = new WorkspaceCheckpointUserResponse { Id = feedback.Creator.Id, Name = feedback.Creator.FullName, Role = ResolveRole(feedback.Creator), AvatarUrl = feedback.Creator.AvatarUrl }
        };
        await realtimePublisher.PublishAsync(await RecipientUserIdsAsync(team, cancellationToken), teamId, response, cancellationToken);
        return Result.Success(response);
    }

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> ExportAsync(Guid teamId, int checkpointNumber, Guid feedbackId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var isAdmin = Is(role, SystemRoles.Admin);
        var isLecturer = Is(role, SystemRoles.Lecturer);
        var isMentor = Is(role, SystemRoles.Mentor);
        if (!isAdmin && !isLecturer && !isMentor) return Result.Failure<(byte[], string, string)>(ErrorCodes.WorkspaceAccessDenied, "You do not have permission to export technical reviews.");
        var team = await context.Teams.AsNoTracking().Include(item => item.Class).ThenInclude(item => item.Course)
            .Include(item => item.Class).ThenInclude(item => item.Semester).Include(item => item.Project)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId && (isAdmin ||
                (isLecturer && (item.Class.PrimaryLecturerId == userId || item.Class.ClassLecturers.Any(link => link.LecturerId == userId))) ||
                (isMentor && item.MentorAssignments.Any(link => link.Status == MentorAssignmentStatus.Active && link.EndedAt == null && link.MentorProfile.UserId == userId))), cancellationToken);
        if (team is null) return Result.Failure<(byte[], string, string)>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var feedback = await context.SubmissionFeedbacks.AsNoTracking().Include(item => item.Creator)
            .FirstOrDefaultAsync(item => item.Id == feedbackId && item.Submission.TeamId == teamId && item.Submission.Checkpoint.CheckpointNumber == checkpointNumber, cancellationToken);
        if (feedback is null || !feedback.Content.StartsWith("TECHNICAL REVIEW\n", StringComparison.Ordinal)) return Result.Failure<(byte[], string, string)>(ErrorCodes.CommonNotFoundError, "Technical review was not found.");
        if (isMentor && feedback.CreatedById != userId) return Result.Failure<(byte[], string, string)>(ErrorCodes.WorkspaceAccessDenied, "Mentors can export only their own technical reviews.");
        var lines = feedback.Content.Split('\n');
        var rows = new List<string[]>();
        foreach (var line in lines.Skip(4))
        {
            if (line.StartsWith("• "))
            {
                var parts = line[2..].Split(": ", 2);
                if (parts.Length == 2) rows.Add(parts);
            }
            else if (rows.Count > 0) rows[^1][1] += $"\n{line}";
        }
        using var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add("Technical Review");
        sheet.Cell(1, 1).Value = "STARTUP TEAM REVIEW"; sheet.Range(1, 1, 1, 4).Merge().Style.Font.SetBold().Font.SetFontSize(16); sheet.Range(1, 1, 1, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Cell(3, 1).Value = "Project / Team"; sheet.Cell(3, 2).Value = $"{team.Project?.Name ?? team.TeamName} / {team.TeamName}";
        sheet.Cell(4, 1).Value = "Review date"; sheet.Cell(4, 2).Value = lines.ElementAtOrDefault(1)?.Replace("Review date: ", string.Empty) ?? string.Empty;
        sheet.Cell(5, 1).Value = "Reviewer"; sheet.Cell(5, 2).Value = feedback.Creator?.FullName ?? "";
        sheet.Cell(7, 1).Value = "Criteria"; sheet.Cell(7, 2).Value = "Assessment"; sheet.Cell(7, 3).Value = "Evidence"; sheet.Cell(7, 4).Value = "Feedback / issues to fix";
        sheet.Range(7, 1, 7, 4).Style.Font.SetBold(); sheet.Range(7, 1, 7, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
        var rowIndex = 8; foreach (var row in rows) { var labelAndEvidence = row[0].Split(" (", 2); sheet.Cell(rowIndex, 1).Value = labelAndEvidence[0]; sheet.Cell(rowIndex, 2).Value = AssessmentFor(labelAndEvidence[0]); sheet.Cell(rowIndex, 3).Value = labelAndEvidence.Length > 1 ? labelAndEvidence[1].TrimEnd(')') : string.Empty; sheet.Cell(rowIndex++, 4).Value = row[1]; }
        sheet.Range(7, 1, Math.Max(7, rowIndex - 1), 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; sheet.Range(7, 1, Math.Max(7, rowIndex - 1), 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin; sheet.Columns().AdjustToContents(); sheet.Column(2).Width = 45; sheet.Column(4).Width = 55; sheet.Range(1, 1, rowIndex, 4).Style.Alignment.WrapText = true;
        using var stream = new MemoryStream(); workbook.SaveAs(stream);
        var fileName = $"{Safe(feedback.Creator?.FullName ?? "Lecturer")}_{Safe(team.Class.Course.Code)}_{Safe(team.TeamName)}_Review Startup_{Safe(team.Class.Semester.Code)}.xlsx";
        return Result.Success((stream.ToArray(), ExcelContentType, fileName));
    }
    private static string Safe(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    private static string AssessmentFor(string criterion) => criterion switch
    {
        "Core functionality" => "The core user flow works and meets the selected requirements.",
        "Interface and user experience" => "Easy to understand and use, with an appropriate display across devices.",
        "Technology and architecture" => "The chosen technology is suitable and the team can explain how the system works.",
        "Data and basic security" => "Input validation, appropriate permissions, and protection for passwords and API keys.",
        "Testing and improvement" => "Core functions are tested; issues are logged and verified after fixes.",
        _ => string.Empty
    };

    private async Task<IReadOnlyCollection<Guid>> RecipientUserIdsAsync(Team team, CancellationToken ct)
    {
        var admins = await context.Users.AsNoTracking().Where(user => user.UserRoles.Any(link => link.Role.Name == SystemRoles.Admin)).Select(user => user.Id).ToArrayAsync(ct);
        return admins.Concat(team.TeamMembers.Where(member => member.CountsTowardActiveTeam && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active).Select(member => member.ClassStudent.Student.UserId).Where(id => id.HasValue).Select(id => id!.Value))
            .Concat(team.MentorAssignments.Where(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null).Select(item => item.MentorProfile.UserId))
            .Concat(team.Class.ClassLecturers.Select(item => item.LecturerId)).Append(team.Class.PrimaryLecturerId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
    }
    private static bool CanAccess(Team team, Guid userId, string role) => Is(role, SystemRoles.Admin) || (Is(role, SystemRoles.Lecturer) && (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(item => item.LecturerId == userId))) || (Is(role, SystemRoles.Mentor) && team.MentorAssignments.Any(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null && item.MentorProfile.UserId == userId)) || (Is(role, SystemRoles.Student) && team.TeamMembers.Any(item => item.CountsTowardActiveTeam && item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && item.ClassStudent.Student.UserId == userId));
    private static string ResolveRole(User user) => user.UserRoles.Select(item => item.Role.Name).FirstOrDefault(role => Is(role, SystemRoles.Admin) || Is(role, SystemRoles.Lecturer) || Is(role, SystemRoles.Mentor) || Is(role, SystemRoles.Student)) ?? string.Empty;
    private static bool Is(string value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<WorkspaceCheckpointFeedbackResponse> Denied() => Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
}
