using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
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
    IApplicationDbContext context) : IGetWorkspaceCheckpointOverviewQueryHandler
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
        var latestSubmissionIds = latestSubmissions.Values
            .Select(submission => submission.Id)
            .ToArray();
        var files = latestSubmissionIds.Length == 0
            ? Array.Empty<SubmissionFile>()
            : await context.SubmissionFiles
                .AsNoTracking()
                .Include(file => file.UploadedBy)
                .Where(file => latestSubmissionIds.Contains(file.SubmissionId))
                .OrderByDescending(file => file.UploadedAt)
                .ToArrayAsync(cancellationToken);
        var feedbacks = latestSubmissionIds.Length == 0
            ? Array.Empty<SubmissionFeedback>()
            : await context.SubmissionFeedbacks
                .AsNoTracking()
                .Include(feedback => feedback.Creator)
                .ThenInclude(user => user!.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .Where(feedback => latestSubmissionIds.Contains(feedback.SubmissionId))
                .OrderBy(feedback => feedback.CreatedAt)
                .ToArrayAsync(cancellationToken);
        var filesBySubmission = files
            .GroupBy(file => file.SubmissionId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var feedbacksBySubmission = feedbacks
            .GroupBy(feedback => feedback.SubmissionId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return Result.Success(new WorkspaceCheckpointOverviewResponse
        {
            SubjectCode = academicContext.SubjectCode,
            Checkpoints = checkpoints.Select(ToCheckpointResponse).ToArray(),
            Submissions = checkpoints
                .Select(checkpoint => ToSubmissionResponse(
                    checkpoint,
                    latestSubmissions.GetValueOrDefault(checkpoint.Id),
                    FilesForCheckpoint(
                        checkpoint.Id,
                        latestSubmissions,
                        filesBySubmission)))
                .ToArray(),
            Feedbacks = checkpoints
                .SelectMany(checkpoint => ToFeedbackResponses(
                    checkpoint,
                    FeedbacksForCheckpoint(
                        checkpoint.Id,
                        latestSubmissions,
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
            return query.Where(team => team.Class.PrimaryLecturerId == userId);
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

    private static SubjectCheckpointResponse ToCheckpointResponse(Checkpoint checkpoint)
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
        IReadOnlyCollection<SubmissionFile> files)
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
            Files = files
                .OrderByDescending(file => file.UploadedAt)
                .Select(file => new WorkspaceCheckpointFileResponse
                {
                    Id = file.Id,
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
                        Role = ResolveRole(feedback.Creator)
                    }
            });
    }

    private static IReadOnlyCollection<SubmissionFile> FilesForCheckpoint(
        Guid checkpointId,
        IReadOnlyDictionary<Guid, Submission> latestSubmissions,
        IReadOnlyDictionary<Guid, SubmissionFile[]> filesBySubmission)
    {
        var submission = latestSubmissions.GetValueOrDefault(checkpointId);
        return submission is null
            ? Array.Empty<SubmissionFile>()
            : filesBySubmission.GetValueOrDefault(submission.Id) ?? Array.Empty<SubmissionFile>();
    }

    private static IReadOnlyCollection<SubmissionFeedback> FeedbacksForCheckpoint(
        Guid checkpointId,
        IReadOnlyDictionary<Guid, Submission> latestSubmissions,
        IReadOnlyDictionary<Guid, SubmissionFeedback[]> feedbacksBySubmission)
    {
        var submission = latestSubmissions.GetValueOrDefault(checkpointId);
        return submission is null
            ? Array.Empty<SubmissionFeedback>()
            : feedbacksBySubmission.GetValueOrDefault(submission.Id) ?? Array.Empty<SubmissionFeedback>();
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

    private static Result<WorkspaceCheckpointOverviewResponse> AccessDenied() =>
        Result.Failure<WorkspaceCheckpointOverviewResponse>(new Error(
            ErrorCodes.WorkspaceAccessDenied,
            "You do not have access to checkpoints for this team workspace."));
}
