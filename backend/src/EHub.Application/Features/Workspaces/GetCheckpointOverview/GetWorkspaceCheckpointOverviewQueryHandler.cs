using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Subjects;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.GetCheckpointOverview;

public sealed class GetWorkspaceCheckpointOverviewQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IGetWorkspaceCheckpointOverviewQueryHandler
{
    public async Task<Result<WorkspaceCheckpointOverviewResponse>> HandleAsync(
        Guid teamId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupportedRole(role))
        {
            return AccessDenied();
        }

        var accessibleTeams = AccessibleTeams(userId, role);
        if (accessibleTeams is null)
        {
            return AccessDenied();
        }

        var academicContext = await accessibleTeams
            .Where(team => team.Id == teamId)
            .Select(team => new
            {
                team.ClassId,
                team.Class.CourseId,
                SubjectCode = team.Class.Course.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (academicContext is null)
        {
            return AccessDenied();
        }

        var checkpoints = await context.Checkpoints
            .AsNoTracking()
            .Include(checkpoint => checkpoint.Rubrics)
            .ThenInclude(rubric => rubric.Criteria)
            .Where(checkpoint =>
                checkpoint.CourseId == academicContext.CourseId &&
                checkpoint.ClassId == null &&
                checkpoint.Status != CheckpointStatus.Archived)
            .OrderBy(checkpoint => checkpoint.CheckpointNumber)
            .ToListAsync(cancellationToken);

        var checkpointIds = checkpoints.Select(checkpoint => checkpoint.Id).ToArray();
        var schedules = checkpointIds.Length == 0
            ? Array.Empty<ClassCheckpointSchedule>()
            : await context.ClassCheckpointSchedules
                .AsNoTracking()
                .Where(item => item.ClassId == academicContext.ClassId && checkpointIds.Contains(item.CheckpointId))
                .ToArrayAsync(cancellationToken);
        var scheduleByCheckpoint = schedules.ToDictionary(item => item.CheckpointId);
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var submissions = checkpointIds.Length == 0
            ? Array.Empty<Submission>()
            : await context.Submissions
                .AsNoTracking()
                .Where(submission =>
                    submission.TeamId == teamId &&
                    checkpointIds.Contains(submission.CheckpointId))
                .OrderByDescending(submission => submission.VersionNumber)
                .ThenByDescending(submission => submission.CreatedAt)
                .ToArrayAsync(cancellationToken);

        var latestSubmissions = submissions
            .GroupBy(submission => submission.CheckpointId)
            .ToDictionary(group => group.Key, group => group.First());
        var submissionsByCheckpoint = submissions
            .GroupBy(submission => submission.CheckpointId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var latestSubmissionIds = latestSubmissions.Values
            .Select(submission => submission.Id)
            .ToArray();
        var requirementContents = latestSubmissionIds.Length == 0
            ? Array.Empty<SubmissionRequirementContent>()
            : await context.SubmissionRequirementContents
                .AsNoTracking()
                .Where(content => latestSubmissionIds.Contains(content.SubmissionId))
                .OrderBy(content => content.RequirementIndex)
                .ToArrayAsync(cancellationToken);
        var submissionIds = submissions.Select(submission => submission.Id).ToArray();
        var files = submissionIds.Length == 0
            ? Array.Empty<SubmissionFile>()
            : await context.SubmissionFiles
                .AsNoTracking()
                .Include(file => file.UploadedBy)
                .Include(file => file.Submission)
                .Where(file => submissionIds.Contains(file.SubmissionId))
                .OrderByDescending(file => file.UploadedAt)
                .ToArrayAsync(cancellationToken);
        var feedbacks = submissionIds.Length == 0
            ? Array.Empty<SubmissionFeedback>()
            : await context.SubmissionFeedbacks
                .AsNoTracking()
                .Include(feedback => feedback.Creator)
                .ThenInclude(user => user!.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .Where(feedback => submissionIds.Contains(feedback.SubmissionId))
                .OrderBy(feedback => feedback.CreatedAt)
                .ToArrayAsync(cancellationToken);
        var filesBySubmission = files
            .GroupBy(file => file.SubmissionId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var feedbacksBySubmission = feedbacks
            .GroupBy(feedback => feedback.SubmissionId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var requirementContentsBySubmission = requirementContents
            .GroupBy(content => content.SubmissionId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        return Result.Success(new WorkspaceCheckpointOverviewResponse
        {
            SubjectCode = academicContext.SubjectCode,
            Checkpoints = checkpoints
                .Select(checkpoint => ToCheckpointResponse(
                    checkpoint,
                    scheduleByCheckpoint.GetValueOrDefault(checkpoint.Id),
                    now))
                .ToArray(),
            Submissions = checkpoints
                .Select(checkpoint => ToSubmissionResponse(
                    checkpoint,
                    latestSubmissions.GetValueOrDefault(checkpoint.Id),
                    FilesForCheckpoint(
                        checkpoint.Id,
                        submissionsByCheckpoint,
                        filesBySubmission),
                    RequirementContentsForCheckpoint(
                        checkpoint.Id,
                        latestSubmissions,
                        requirementContentsBySubmission)))
                .ToArray(),
            Feedbacks = checkpoints
                .SelectMany(checkpoint => ToFeedbackResponses(
                    checkpoint,
                    FeedbacksForCheckpoint(
                        checkpoint.Id,
                        submissionsByCheckpoint,
                        feedbacksBySubmission)))
                .ToArray()
        });
    }

    private IQueryable<Team>? AccessibleTeams(Guid userId, string role)
    {
        var query = context.Teams.AsNoTracking();
        if (IsRole(role, SystemRoles.Admin))
        {
            return query;
        }

        if (IsRole(role, SystemRoles.Lecturer))
        {
            return query.Where(team => team.Class.PrimaryLecturerId == userId ||
                team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId));
        }

        if (IsRole(role, SystemRoles.Mentor))
        {
            return query.Where(team => team.MentorAssignments.Any(assignment =>
                assignment.MentorProfile.UserId == userId &&
                assignment.Status == MentorAssignmentStatus.Active &&
                assignment.EndedAt == null));
        }

        if (IsRole(role, SystemRoles.Student))
        {
            return query.Where(team => team.TeamMembers.Any(member =>
                member.CountsTowardActiveTeam &&
                member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
                member.ClassStudent.Student.UserId == userId));
        }

        return null;
    }

    private static SubjectCheckpointResponse ToCheckpointResponse(
        Checkpoint checkpoint,
        ClassCheckpointSchedule? schedule,
        DateTime now)
    {
        var rubric = checkpoint.Rubrics
            .Where(item => item.ClassId == null)
            .OrderBy(item => item.Name)
            .FirstOrDefault();

        return new SubjectCheckpointResponse
        {
            Number = checkpoint.CheckpointNumber,
            Title = checkpoint.Name,
            ShortDescription = checkpoint.Description,
            StartDateUtc = schedule?.StartDateUtc,
            EndDateUtc = schedule?.EndDateUtc,
            ScheduleStatus = ScheduleStatus(schedule, now),
            CanUpload = schedule is not null && now >= schedule.StartDateUtc && now <= schedule.EndDateUtc,
            Requirements = DeserializeArray<string>(checkpoint.RequirementsJson),
            Rubrics = rubric?.Criteria
                .OrderBy(criterion => criterion.DisplayOrder)
                .Select(criterion => new SubjectCriterionResponse
                {
                    Key = string.IsNullOrWhiteSpace(criterion.Key)
                        ? criterion.Name
                        : criterion.Key,
                    Label = criterion.Name,
                    Description = criterion.Description,
                    Weight = criterion.Weight,
                    Levels = DeserializeArray<object>(criterion.LevelsJson).ToArray()
                })
                .ToArray() ?? Array.Empty<SubjectCriterionResponse>()
        };
    }

    private static WorkspaceCheckpointSubmissionResponse ToSubmissionResponse(
        Checkpoint checkpoint,
        Submission? submission,
        IReadOnlyCollection<SubmissionFile> files,
        IReadOnlyCollection<SubmissionRequirementContent> requirementContents)
    {
        if (submission is null)
        {
            return new WorkspaceCheckpointSubmissionResponse
            {
                CheckpointNumber = checkpoint.CheckpointNumber,
                Status = "NotSubmitted"
            };
        }

        return new WorkspaceCheckpointSubmissionResponse
        {
            CheckpointNumber = checkpoint.CheckpointNumber,
            Status = submission.Status.ToString(),
            SubmittedAt = submission.SubmittedAt,
            Files = files
                .OrderByDescending(file => file.VersionNumber)
                .ThenByDescending(file => file.UploadedAt)
                .Select(file => new WorkspaceCheckpointFileResponse
                {
                    Id = file.Id,
                    VersionNumber = file.VersionNumber,
                    OriginalName = file.OriginalName,
                    FileType = GetFileType(file.OriginalName),
                    FileSize = file.FileSize,
                    UploadedAt = file.UploadedAt,
                    UploadedBy = file.UploadedBy is null
                        ? null
                        : new WorkspaceCheckpointUserResponse
                        {
                            Id = file.UploadedBy.Id,
                            Name = file.UploadedBy.FullName
                        }
                })
                .ToArray(),
            RequirementContents = requirementContents
                .OrderBy(content => content.RequirementIndex)
                .Select(content => new WorkspaceCheckpointRequirementContentResponse
                {
                    Index = content.RequirementIndex,
                    Content = content.Content
                })
                .ToArray()
        };
    }

    private static IEnumerable<WorkspaceCheckpointFeedbackResponse> ToFeedbackResponses(
        Checkpoint checkpoint,
        IReadOnlyCollection<SubmissionFeedback> feedbacks)
    {
        return feedbacks
            .OrderBy(feedback => feedback.CreatedAt)
            .Select(feedback => new WorkspaceCheckpointFeedbackResponse
            {
                Id = feedback.Id,
                CheckpointNumber = checkpoint.CheckpointNumber,
                Comment = feedback.Content,
                ParentFeedbackId = feedback.ParentFeedbackId,
                CreatedAt = feedback.CreatedAt,
                User = feedback.Creator is null
                    ? null
                    : new WorkspaceCheckpointUserResponse
                    {
                        Id = feedback.Creator.Id,
                        Name = feedback.Creator.FullName,
                        Role = ResolveRole(feedback.Creator),
                        AvatarUrl = feedback.Creator.AvatarUrl
                    }
            });
    }

    private static IReadOnlyCollection<SubmissionFile> FilesForCheckpoint(
        Guid checkpointId,
        IReadOnlyDictionary<Guid, Submission[]> submissionsByCheckpoint,
        IReadOnlyDictionary<Guid, SubmissionFile[]> filesBySubmission)
    {
        return (submissionsByCheckpoint.GetValueOrDefault(checkpointId) ?? Array.Empty<Submission>())
            .SelectMany(submission => filesBySubmission.GetValueOrDefault(submission.Id) ?? Array.Empty<SubmissionFile>())
            .ToArray();
    }

    private static IReadOnlyCollection<SubmissionFeedback> FeedbacksForCheckpoint(
        Guid checkpointId,
        IReadOnlyDictionary<Guid, Submission[]> submissionsByCheckpoint,
        IReadOnlyDictionary<Guid, SubmissionFeedback[]> feedbacksBySubmission)
    {
        return (submissionsByCheckpoint.GetValueOrDefault(checkpointId) ?? Array.Empty<Submission>())
            .SelectMany(submission => feedbacksBySubmission.GetValueOrDefault(submission.Id) ?? Array.Empty<SubmissionFeedback>())
            .ToArray();
    }

    private static IReadOnlyCollection<SubmissionRequirementContent> RequirementContentsForCheckpoint(
        Guid checkpointId,
        IReadOnlyDictionary<Guid, Submission> latestSubmissions,
        IReadOnlyDictionary<Guid, SubmissionRequirementContent[]> contentsBySubmission)
    {
        var submission = latestSubmissions.GetValueOrDefault(checkpointId);
        return submission is null
            ? Array.Empty<SubmissionRequirementContent>()
            : contentsBySubmission.GetValueOrDefault(submission.Id) ?? Array.Empty<SubmissionRequirementContent>();
    }

    private static IReadOnlyCollection<T> DeserializeArray<T>(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<T[]>(value) ?? Array.Empty<T>();
        }
        catch (JsonException)
        {
            return Array.Empty<T>();
        }
    }

    private static string GetFileType(string originalName)
    {
        var extension = Path.GetExtension(originalName).TrimStart('.');
        return string.IsNullOrWhiteSpace(extension)
            ? "file"
            : extension.ToLowerInvariant();
    }

    private static string ResolveRole(User user)
    {
        var roles = user.UserRoles.Select(userRole => userRole.Role.Name).ToArray();
        if (roles.Any(role => IsRole(role, SystemRoles.Admin)))
        {
            return SystemRoles.Admin;
        }

        if (roles.Any(role => IsRole(role, SystemRoles.Lecturer)))
        {
            return SystemRoles.Lecturer;
        }

        if (roles.Any(role => IsRole(role, SystemRoles.Mentor)))
        {
            return SystemRoles.Mentor;
        }

        if (roles.Any(role => IsRole(role, SystemRoles.Student)))
        {
            return SystemRoles.Student;
        }

        return string.Empty;
    }

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedRole(string role) =>
        IsRole(role, SystemRoles.Admin) ||
        IsRole(role, SystemRoles.Lecturer) ||
        IsRole(role, SystemRoles.Mentor) ||
        IsRole(role, SystemRoles.Student);

    private static string ScheduleStatus(ClassCheckpointSchedule? schedule, DateTime now)
    {
        if (schedule is null) return "NotScheduled";
        if (now < schedule.StartDateUtc) return "Upcoming";
        return now <= schedule.EndDateUtc ? "Open" : "Closed";
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static Result<WorkspaceCheckpointOverviewResponse> AccessDenied() =>
        Result.Failure<WorkspaceCheckpointOverviewResponse>(new Error(
            ErrorCodes.WorkspaceAccessDenied,
            "You do not have access to checkpoints for this team workspace."));
}
