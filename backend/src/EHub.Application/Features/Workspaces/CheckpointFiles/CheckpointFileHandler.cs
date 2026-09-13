using System.IO.Compression;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

public sealed class CheckpointFileHandler(
    IApplicationDbContext context,
    ISubmissionFileStorageService storage) : ICheckpointFileHandler
{
    private const long MaximumFileSize = 15 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public async Task<Result<WorkspaceCheckpointFileResponse>> UploadAsync(Guid teamId, int checkpointNumber, Stream content, string originalName, string contentType, long length, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsStudent(role)) return Denied<WorkspaceCheckpointFileResponse>();
        if (length is <= 0 or > MaximumFileSize) return Invalid<WorkspaceCheckpointFileResponse>("Files must be between 1 byte and 15 MB.");
        var extension = Path.GetExtension(originalName);
        if (!AllowedTypes.TryGetValue(extension, out var expectedContentType))
            return Invalid<WorkspaceCheckpointFileResponse>("Only PDF, DOCX, and PPTX files are accepted.");

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (!HasExpectedSignature(bytes, extension)) return Invalid<WorkspaceCheckpointFileResponse>("The uploaded file does not match its declared format.");

        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(access.Error);
        if (!access.Value.IsMember) return Denied<WorkspaceCheckpointFileResponse>();

        await using var uploadStream = new MemoryStream(bytes, writable: false);
        var storageResult = await storage.UploadAsync(uploadStream, SafeOriginalName(originalName), expectedContentType, teamId, checkpointNumber, cancellationToken);
        if (storageResult.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(storageResult.Error);

        try
        {
            var submission = await context.Submissions
                .OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
            var now = DateTime.UtcNow;
            if (submission is null)
            {
                submission = new Submission
                {
                    ProjectId = access.Value.Project.Id,
                    TeamId = teamId,
                    CheckpointId = access.Value.Checkpoint.Id,
                    SubmittedById = userId,
                    Title = access.Value.Checkpoint.Name,
                    Status = SubmissionStatus.Submitted,
                    SubmittedAt = now,
                    VersionNumber = 1,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                context.Submissions.Add(submission);
            }
            else
            {
                submission.Status = SubmissionStatus.Submitted;
                submission.SubmittedAt = now;
                submission.SubmittedById = userId;
                submission.UpdatedAt = now;
                submission.UpdatedBy = userId;
            }

            var file = new SubmissionFile
            {
                Submission = submission,
                FileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}",
                OriginalName = SafeOriginalName(originalName),
                FileUrl = storageResult.Value.SecureUrl,
                CloudinaryPublicId = storageResult.Value.PublicId,
                MimeType = expectedContentType,
                FileSize = bytes.LongLength,
                FileType = extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ? SubmissionFileType.PitchDeck : SubmissionFileType.Report,
                UploadedById = userId,
                UploadedAt = now,
                CreatedAt = now,
                CreatedBy = userId
            };
            context.SubmissionFiles.Add(file);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(Map(file, access.Value.UserName));
        }
        catch
        {
            await storage.DeleteAsync(storageResult.Value.PublicId, cancellationToken);
            throw;
        }
    }

    public async Task<Result<CheckpointFileDownload>> DownloadAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<CheckpointFileDownload>(access.Error);
        var file = await context.SubmissionFiles.AsNoTracking()
            .Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == fileId && item.Submission.TeamId == teamId && item.Submission.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
        if (file is null) return Result.Failure<CheckpointFileDownload>(ErrorCodes.CommonNotFoundError, "Submitted file was not found.");
        var download = await storage.DownloadAsync(file.FileUrl, cancellationToken);
        return download.IsFailure
            ? Result.Failure<CheckpointFileDownload>(download.Error)
            : Result.Success(new CheckpointFileDownload(download.Value.Content, file.MimeType, file.OriginalName));
    }

    public async Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsStudent(role)) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "Only team students can delete submitted files.");
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);
        if (!access.Value.IsMember) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var file = await context.SubmissionFiles.Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == fileId && item.Submission.TeamId == teamId && item.Submission.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
        if (file is null) return Result.Failure(ErrorCodes.CommonNotFoundError, "Submitted file was not found.");
        if (file.UploadedById != userId) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only delete files you uploaded.");
        file.IsDeleted = true; file.DeletedAt = DateTime.UtcNow; file.DeletedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(file.CloudinaryPublicId, cancellationToken);
        return Result.Success();
    }

    private async Task<Result<Access>> ResolveAccessAsync(Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (!IsSupportedRole(role)) return Result.Failure<Access>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var team = await context.Teams.AsNoTracking().Include(item => item.Class).ThenInclude(item => item.Course)
            .Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return Result.Failure<Access>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var isMember = team.TeamMembers.Any(member => member.CountsTowardActiveTeam && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && member.ClassStudent.Student.UserId == userId);
        var allowed = IsRole(role, SystemRoles.Admin)
            || (IsRole(role, SystemRoles.Lecturer) && (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId)))
            || (IsRole(role, SystemRoles.Mentor) && team.MentorAssignments.Any(assignment => assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null && assignment.MentorProfile.UserId == userId))
            || (IsRole(role, SystemRoles.Student) && isMember);
        if (!allowed) return Result.Failure<Access>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.CourseId == team.Class.CourseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber && item.Status != CheckpointStatus.Archived, cancellationToken);
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (checkpoint is null || project is null) return Result.Failure<Access>(ErrorCodes.WorkspaceNotFound, "The checkpoint workspace was not found.");
        return Result.Success(new Access(checkpoint, project, isMember, team.TeamMembers.FirstOrDefault(member => member.ClassStudent.Student.UserId == userId)?.ClassStudent.Student.FullName ?? string.Empty));
    }

    private static bool HasExpectedSignature(byte[] bytes, string extension)
    {
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B) return false;
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? zip.GetEntry("word/document.xml") is not null
                : zip.GetEntry("ppt/presentation.xml") is not null;
        }
        catch (InvalidDataException) { return false; }
    }

    private static string SafeOriginalName(string name) => Path.GetFileName(name).Trim()[..Math.Min(Path.GetFileName(name).Trim().Length, 256)];
    private static WorkspaceCheckpointFileResponse Map(SubmissionFile file, string userName) => new() { Id = file.Id, OriginalName = file.OriginalName, FileType = file.FileType.ToString(), FileSize = file.FileSize, UploadedAt = file.UploadedAt, UploadedBy = new WorkspaceCheckpointUserResponse { Id = file.UploadedById!.Value, Name = userName, Role = SystemRoles.Student } };
    private static bool IsSupportedRole(string role) => IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer) || IsRole(role, SystemRoles.Mentor) || IsRole(role, SystemRoles.Student);
    private static bool IsStudent(string role) => IsRole(role, SystemRoles.Student);
    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<T> Denied<T>() => Result.Failure<T>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
    private static Result<T> Invalid<T>(string message) => Result.Failure<T>(ErrorCodes.WorkspaceValidationError, message);
    private sealed record Access(Checkpoint Checkpoint, Project Project, bool IsMember, string UserName);
}
