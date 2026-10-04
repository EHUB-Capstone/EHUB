using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

/// <summary>
/// Direct-to-R2 upload: initiate hands the browser a presigned PUT URL, complete verifies the stored
/// object and only then creates the Submission/SubmissionFile.
/// </summary>
public sealed class CheckpointFileUploadHandler(
    IApplicationDbContext context,
    ISubmissionObjectStorage storage,
    IDateTimeProvider dateTimeProvider,
    IValidator<InitiateCheckpointFileUploadRequest> validator) : ICheckpointFileUploadHandler
{
    public async Task<Result<CheckpointFileUploadSessionResponse>> InitiateAsync(
        Guid teamId, int checkpointNumber, InitiateCheckpointFileUploadRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!CheckpointWorkspaceAccess.IsStudent(role)) return Denied<CheckpointFileUploadSessionResponse>();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Invalid<CheckpointFileUploadSessionResponse>(validation.Errors[0].ErrorMessage);

        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<CheckpointFileUploadSessionResponse>(access.Error);
        if (!access.Value.IsMember) return Denied<CheckpointFileUploadSessionResponse>();
        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        var availability = CheckpointWorkspaceAccess.EnsureOpen(access.Value.Schedule, now);
        if (availability.IsFailure) return Result.Failure<CheckpointFileUploadSessionResponse>(availability.Error);

        var pending = await context.SubmissionUploadSessions.CountAsync(item =>
            item.TeamId == teamId && item.UserId == userId &&
            item.Status == SubmissionUploadSessionStatus.Pending && item.ExpiresAtUtc > now, cancellationToken);
        if (pending >= SubmissionFileLimits.MaxPendingUploadSessionsPerUserTeam)
        {
            return Result.Failure<CheckpointFileUploadSessionResponse>(
                ErrorCodes.WorkspaceUploadTooManyPending,
                "Too many uploads are in progress. Wait for them to finish and try again.");
        }

        var originalName = Path.GetFileName(request.FileName.Trim());
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        CheckpointFileTypes.TryGetContentType(extension, out var contentType);

        var session = new SubmissionUploadSession
        {
            TeamId = teamId,
            CheckpointId = access.Value.Checkpoint.Id,
            UserId = userId,
            ObjectKey = $"submissions/{teamId:N}/checkpoint-{checkpointNumber}/{Guid.NewGuid():N}{extension}",
            OriginalName = originalName,
            ContentType = contentType,
            DeclaredSize = request.Size,
            Status = SubmissionUploadSessionStatus.Pending,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(SubmissionFileLimits.UploadSessionLifetime)
        };
        var presigned = storage.CreatePresignedUpload(
            session.ObjectKey, contentType, request.Size, SubmissionFileLimits.PresignedUploadLifetime);
        context.SubmissionUploadSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CheckpointFileUploadSessionResponse
        {
            UploadId = session.Id,
            UploadUrl = presigned.Url,
            Method = "PUT",
            Headers = presigned.Headers,
            UrlExpiresAt = now.Add(SubmissionFileLimits.PresignedUploadLifetime),
            SessionExpiresAt = session.ExpiresAtUtc,
            MaxFileSize = SubmissionFileLimits.MaxFileSizeBytes
        });
    }

    public async Task<Result<WorkspaceCheckpointFileResponse>> CompleteAsync(
        Guid teamId, int checkpointNumber, Guid uploadId,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!CheckpointWorkspaceAccess.IsStudent(role)) return Denied<WorkspaceCheckpointFileResponse>();
        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(access.Error);
        if (!access.Value.IsMember) return Denied<WorkspaceCheckpointFileResponse>();

        var session = await context.SubmissionUploadSessions.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == uploadId && item.UserId == userId && item.TeamId == teamId &&
            item.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
        if (session is null) return NotFound();

        // A retried complete returns the file created by the first call, even after the deadline.
        if (session.Status == SubmissionUploadSessionStatus.Completed) return await ExistingFileAsync(session, access.Value.UserName, cancellationToken);
        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        if (session.Status != SubmissionUploadSessionStatus.Pending || now > session.ExpiresAtUtc) return SessionExpired();
        var availability = CheckpointWorkspaceAccess.EnsureOpen(access.Value.Schedule, now);
        if (availability.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(availability.Error);

        var info = await storage.GetObjectInfoAsync(session.ObjectKey, cancellationToken);
        if (info.IsFailure)
        {
            return info.Error.Code == ErrorCodes.CommonNotFoundError
                ? Result.Failure<WorkspaceCheckpointFileResponse>(ErrorCodes.WorkspaceUploadObjectMissing, "The file has not finished uploading. Please retry.")
                : Result.Failure<WorkspaceCheckpointFileResponse>(info.Error);
        }

        if (info.Value.Size <= 0 || info.Value.Size > SubmissionFileLimits.MaxFileSizeBytes)
        {
            return await AbortAsync(session.Id, session.ObjectKey,
                $"Files must be between 1 byte and {SubmissionFileLimits.MaxFileSizeBytes / (1024 * 1024)} MB.", cancellationToken);
        }

        if (info.Value.Size != session.DeclaredSize)
        {
            return await AbortAsync(session.Id, session.ObjectKey, "The uploaded file size does not match the declared size.", cancellationToken);
        }

        var extension = Path.GetExtension(session.OriginalName).ToLowerInvariant();
        var header = await storage.ReadRangeAsync(session.ObjectKey, 0, CheckpointFileTypes.HeaderLength, cancellationToken);
        if (header.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(header.Error);
        if (!CheckpointFileTypes.HasExpectedHeader(header.Value, extension))
        {
            return await AbortAsync(session.Id, session.ObjectKey, "The uploaded file does not match its declared format.", cancellationToken);
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            int? attemptedVersion = null;
            Guid? reusedDraftId = null;
            uint? observedRowVersion = null;
            try
            {
                var tracked = await context.SubmissionUploadSessions.FirstOrDefaultAsync(item => item.Id == uploadId, cancellationToken);
                if (tracked is null) return NotFound();
                if (tracked.Status == SubmissionUploadSessionStatus.Completed) return await ExistingFileAsync(tracked, access.Value.UserName, cancellationToken);
                if (tracked.Status != SubmissionUploadSessionStatus.Pending) return SessionExpired();

                var latest = await context.Submissions
                    .Include(item => item.RequirementContents)
                    .OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt)
                    .FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
                var highestFileVersion = await context.SubmissionFiles.IgnoreQueryFilters().AsNoTracking()
                    .Where(item => item.Submission.ProjectId == access.Value.Project.Id &&
                        item.Submission.CheckpointId == access.Value.Checkpoint.Id)
                    .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0;
                var highestLinkVersion = await context.SubmissionLinks.IgnoreQueryFilters().AsNoTracking()
                    .Where(item => item.Submission.ProjectId == access.Value.Project.Id &&
                        item.Submission.CheckpointId == access.Value.Checkpoint.Id)
                    .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0;
                var highestArtifactVersion = Math.Max(highestFileVersion, highestLinkVersion);
                var reuseDraft = latest is not null && latest.Status == SubmissionStatus.Draft &&
                    latest.SubmittedAt is null && latest.VersionNumber > highestArtifactVersion &&
                    !await context.SubmissionFiles.AnyAsync(item => item.SubmissionId == latest.Id, cancellationToken) &&
                    !await context.SubmissionLinks.AnyAsync(item => item.SubmissionId == latest.Id, cancellationToken);
                Submission submission;
                if (reuseDraft)
                {
                    submission = latest!;
                    reusedDraftId = submission.Id;
                    observedRowVersion = submission.RowVersion;
                    submission.Status = SubmissionStatus.Submitted;
                    submission.SubmittedAt = now;
                    submission.SubmittedById = userId;
                    submission.UpdatedAt = now;
                    submission.UpdatedBy = userId;
                }
                else
                {
                    var highestVersion = await context.Submissions.IgnoreQueryFilters().AsNoTracking()
                        .Where(item => item.ProjectId == access.Value.Project.Id &&
                            item.CheckpointId == access.Value.Checkpoint.Id)
                        .MaxAsync(item => (int?)item.VersionNumber, cancellationToken);
                    submission = new Submission
                    {
                        ProjectId = access.Value.Project.Id,
                        TeamId = teamId,
                        CheckpointId = access.Value.Checkpoint.Id,
                        SubmittedById = userId,
                        Title = access.Value.Checkpoint.Name,
                        Status = SubmissionStatus.Submitted,
                        SubmittedAt = now,
                        VersionNumber = Math.Max(highestVersion ?? 0, highestArtifactVersion) + 1,
                        CreatedAt = now,
                        CreatedBy = userId
                    };
                    if (latest is not null)
                    {
                        foreach (var previous in latest.RequirementContents)
                        {
                            submission.RequirementContents.Add(new SubmissionRequirementContent
                            {
                                RequirementIndex = previous.RequirementIndex,
                                Content = previous.Content,
                                CreatedAt = now,
                                CreatedBy = userId
                            });
                        }
                    }
                    context.Submissions.Add(submission);
                }
                attemptedVersion = submission.VersionNumber;

                var file = new SubmissionFile
                {
                    Submission = submission,
                    FileName = $"{Guid.NewGuid():N}{extension}",
                    OriginalName = session.OriginalName,
                    VersionNumber = submission.VersionNumber,
                    FileUrl = string.Empty,
                    CloudinaryPublicId = string.Empty,
                    StorageProvider = SubmissionStorageProvider.R2,
                    StorageKey = session.ObjectKey,
                    MimeType = session.ContentType,
                    FileSize = info.Value.Size,
                    // Convert DOCX/PPTX in the background right away so Preview does not have to wait for LibreOffice.
                    // Saved in the same SaveChanges as the file itself, so a file is never left without its queue entry.
                    PreviewStatus = CheckpointFileTypes.NeedsBackgroundPreview(extension, info.Value.Size)
                        ? SubmissionPreviewStatus.Pending
                        : SubmissionPreviewStatus.None,
                    PreviewNextAttemptAtUtc = CheckpointFileTypes.NeedsBackgroundPreview(extension, info.Value.Size) ? now : null,
                    FileType = extension == ".pptx" ? SubmissionFileType.PitchDeck : SubmissionFileType.Report,
                    UploadedById = userId,
                    UploadedAt = now,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                context.SubmissionFiles.Add(file);
                tracked.Status = SubmissionUploadSessionStatus.Completed;
                tracked.CompletedAtUtc = now;
                tracked.SubmissionFileId = file.Id;
                // Session state, Submission and SubmissionFile commit together or not at all.
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(Map(file, access.Value.UserName));
            }
            catch (DbUpdateException)
            {
                context.ClearChanges();
                var alreadyCompleted = await context.SubmissionUploadSessions.AsNoTracking()
                    .AnyAsync(item => item.Id == uploadId && item.Status == SubmissionUploadSessionStatus.Completed, cancellationToken);
                var conflictingVersion = alreadyCompleted || (reusedDraftId.HasValue
                    ? await context.Submissions.AsNoTracking().AnyAsync(item => item.Id == reusedDraftId.Value &&
                        item.RowVersion != observedRowVersion, cancellationToken)
                    : attemptedVersion.HasValue && await context.Submissions.AsNoTracking()
                        .AnyAsync(item => item.ProjectId == access.Value.Project.Id &&
                            item.CheckpointId == access.Value.Checkpoint.Id &&
                            item.VersionNumber == attemptedVersion.Value, cancellationToken));
                if (!conflictingVersion) throw;
            }
        }

        // The object stays in place and the session stays pending, so the browser can retry complete.
        return Result.Failure<WorkspaceCheckpointFileResponse>(ErrorCodes.WorkspaceConcurrencyConflict,
            "Another upload changed this checkpoint at the same time. Please retry.");
    }

    private async Task<Result<WorkspaceCheckpointFileResponse>> AbortAsync(
        Guid sessionId, string objectKey, string message, CancellationToken cancellationToken)
    {
        await storage.DeleteAsync(objectKey, cancellationToken);
        var tracked = await context.SubmissionUploadSessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (tracked is { Status: SubmissionUploadSessionStatus.Pending })
        {
            tracked.Status = SubmissionUploadSessionStatus.Aborted;
            await context.SaveChangesAsync(cancellationToken);
        }

        return Invalid<WorkspaceCheckpointFileResponse>(message);
    }

    private async Task<Result<WorkspaceCheckpointFileResponse>> ExistingFileAsync(
        SubmissionUploadSession session, string userName, CancellationToken cancellationToken)
    {
        var file = session.SubmissionFileId.HasValue
            ? await context.SubmissionFiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == session.SubmissionFileId.Value, cancellationToken)
            : null;
        return file is null
            ? Result.Failure<WorkspaceCheckpointFileResponse>(ErrorCodes.CommonNotFoundError, "Submitted file was not found.")
            : Result.Success(Map(file, userName));
    }

    private static WorkspaceCheckpointFileResponse Map(SubmissionFile file, string userName) => new()
    {
        Id = file.Id,
        VersionNumber = file.VersionNumber,
        OriginalName = file.OriginalName,
        FileType = file.FileType.ToString(),
        FileSize = file.FileSize,
        CanDirectDownload = CheckpointDownloadPolicy.CanDirectDownload(file),
        UploadedAt = file.UploadedAt,
        UploadedBy = new WorkspaceCheckpointUserResponse { Id = file.UploadedById!.Value, Name = userName, Role = SystemRoles.Student }
    };

    private static Result<WorkspaceCheckpointFileResponse> NotFound() =>
        Result.Failure<WorkspaceCheckpointFileResponse>(ErrorCodes.CommonNotFoundError, "Upload session was not found.");

    private static Result<WorkspaceCheckpointFileResponse> SessionExpired() =>
        Result.Failure<WorkspaceCheckpointFileResponse>(ErrorCodes.WorkspaceUploadSessionExpired, "The upload session has expired. Please upload the file again.");

    private static Result<T> Denied<T>() => Result.Failure<T>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
    private static Result<T> Invalid<T>(string message) => Result.Failure<T>(ErrorCodes.WorkspaceValidationError, message);
}
