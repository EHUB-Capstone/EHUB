using System.IO.Compression;
using System.Collections.Concurrent;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
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
    ISubmissionFileStorageService storage,
    IDateTimeProvider dateTimeProvider,
    IDocumentPreviewConverter previewConverter) : ICheckpointFileHandler
{
    private const long MaximumFileSize = 15 * 1024 * 1024;
    private static readonly ConcurrentDictionary<Guid, PreviewLockEntry> PreviewLocks = new();
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

        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(access.Error);
        if (!access.Value.IsMember) return Denied<WorkspaceCheckpointFileResponse>();
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        if (access.Value.Schedule is null || now < access.Value.Schedule.StartDateUtc || now > access.Value.Schedule.EndDateUtc)
        {
            var message = access.Value.Schedule is null
                ? "This checkpoint has not been scheduled for your class."
                : now < access.Value.Schedule.StartDateUtc
                    ? $"This checkpoint opens at {access.Value.Schedule.StartDateUtc:O}."
                    : $"This checkpoint closed at {access.Value.Schedule.EndDateUtc:O}.";
            return Result.Failure<WorkspaceCheckpointFileResponse>(ErrorCodes.WorkspaceCheckpointNotOpen, message);
        }

        // IFormFile may expose a seekable request stream that still rejects synchronous reads.
        // Buffer asynchronously before inspecting ZIP-based Office formats so validation never
        // performs sync I/O against the ASP.NET request body. The request is already capped at
        // 15 MB above and at the controller boundary.
        await using var bufferedContent = new MemoryStream((int)length);
        await content.CopyToAsync(bufferedContent, cancellationToken);
        bufferedContent.Position = 0;
        if (!HasExpectedSignature(bufferedContent, extension))
        {
            return Invalid<WorkspaceCheckpointFileResponse>("The uploaded file does not match its declared format.");
        }

        bufferedContent.Position = 0;
        var storageResult = await storage.UploadAsync(
            bufferedContent,
            SafeOriginalName(originalName),
            expectedContentType,
            teamId,
            checkpointNumber,
            cancellationToken);
        if (storageResult.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(storageResult.Error);

        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                int? attemptedVersion = null;
                Guid? reusedDraftId = null;
                uint? observedRowVersion = null;
                try
                {
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
                        FileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}",
                        OriginalName = SafeOriginalName(originalName),
                        VersionNumber = submission.VersionNumber,
                        FileUrl = storageResult.Value.SecureUrl,
                        CloudinaryPublicId = storageResult.Value.PublicId,
                        MimeType = expectedContentType,
                        FileSize = length,
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
                catch (DbUpdateException)
                {
                    context.ClearChanges();
                    var conflictingVersion = reusedDraftId.HasValue
                        ? await context.Submissions.AsNoTracking().AnyAsync(item => item.Id == reusedDraftId.Value &&
                            item.RowVersion != observedRowVersion, cancellationToken)
                        : attemptedVersion.HasValue && await context.Submissions.AsNoTracking()
                            .AnyAsync(item => item.ProjectId == access.Value.Project.Id &&
                                item.CheckpointId == access.Value.Checkpoint.Id &&
                                item.VersionNumber == attemptedVersion.Value, cancellationToken);
                    if (!conflictingVersion) throw;
                }
            }
        }
        catch
        {
            await storage.DeleteAsync(storageResult.Value.PublicId, cancellationToken);
            throw;
        }
        await storage.DeleteAsync(storageResult.Value.PublicId, cancellationToken);
        return Result.Failure<WorkspaceCheckpointFileResponse>(ErrorCodes.WorkspaceConcurrencyConflict,
            "Another upload changed this checkpoint at the same time. Please retry.");
    }

    public async Task<Result<CheckpointFileDownload>> DownloadAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: false, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CheckpointFileDownload>(resolved.Error);
        var download = await storage.DownloadAsync(resolved.Value.File.FileUrl, cancellationToken);
        return download.IsFailure
            ? Result.Failure<CheckpointFileDownload>(download.Error)
            : Result.Success(new CheckpointFileDownload(
                download.Value.Content,
                resolved.Value.File.MimeType,
                resolved.Value.File.OriginalName));
    }

    public async Task<Result<CheckpointFilePreview>> PreviewAsync(
        Guid teamId,
        int checkpointNumber,
        Guid fileId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: false, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CheckpointFilePreview>(resolved.Error);

        var extension = Path.GetExtension(resolved.Value.File.OriginalName).ToLowerInvariant();
        if (extension == ".pdf")
        {
            var pdf = await storage.DownloadAsync(resolved.Value.File.FileUrl, cancellationToken);
            if (pdf.IsFailure) return Result.Failure<CheckpointFilePreview>(pdf.Error);
            if (!HasPdfSignature(pdf.Value.Content)) return ConversionFailed<CheckpointFilePreview>();
            return Result.Success(new CheckpointFilePreview(pdf.Value.Content, resolved.Value.File.OriginalName, FromCache: false));
        }

        if (extension is not ".docx" and not ".pptx")
        {
            return Result.Failure<CheckpointFilePreview>(
                ErrorCodes.WorkspaceFilePreviewUnsupported,
                "This file format cannot be previewed. You can still download the original file.");
        }

        using (await AcquirePreviewLockAsync(fileId, cancellationToken))
        {
            context.ClearChanges();
            resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: true, cancellationToken);
            if (resolved.IsFailure) return Result.Failure<CheckpointFilePreview>(resolved.Error);
            var file = resolved.Value.File;

            if (!string.IsNullOrWhiteSpace(file.PreviewPdfUrl) &&
                file.PreviewSourceVersionNumber == file.VersionNumber)
            {
                var cached = await storage.DownloadAsync(file.PreviewPdfUrl, cancellationToken);
                if (cached.IsSuccess && HasPdfSignature(cached.Value.Content))
                {
                    return Result.Success(new CheckpointFilePreview(cached.Value.Content, PreviewName(file), FromCache: true));
                }
            }

            var original = await storage.DownloadAsync(file.FileUrl, cancellationToken);
            if (original.IsFailure) return Result.Failure<CheckpointFilePreview>(original.Error);
            var converted = await previewConverter.ConvertToPdfAsync(original.Value.Content, extension, cancellationToken);
            if (converted.IsFailure) return Result.Failure<CheckpointFilePreview>(converted.Error);

            await using var previewStream = new MemoryStream(converted.Value.Content, writable: false);
            var uploaded = await storage.UploadAsync(
                previewStream,
                PreviewName(file),
                "application/pdf",
                teamId,
                checkpointNumber,
                cancellationToken);
            if (uploaded.IsFailure)
            {
                return Result.Failure<CheckpointFilePreview>(
                    ErrorCodes.WorkspaceFilePreviewUnavailable,
                    "The PDF preview could not be cached. You can still download the original file.");
            }

            var previousPreviewPublicId = file.PreviewPdfPublicId;
            file.PreviewPdfUrl = uploaded.Value.SecureUrl;
            file.PreviewPdfPublicId = uploaded.Value.PublicId;
            file.PreviewSourceVersionNumber = file.VersionNumber;
            file.PreviewGeneratedAt = EnsureUtc(dateTimeProvider.UtcNow);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await storage.DeleteAsync(uploaded.Value.PublicId, cancellationToken);
                throw;
            }

            if (!string.IsNullOrWhiteSpace(previousPreviewPublicId) &&
                !string.Equals(previousPreviewPublicId, uploaded.Value.PublicId, StringComparison.Ordinal))
            {
                await storage.DeleteAsync(previousPreviewPublicId, cancellationToken);
            }

            return Result.Success(new CheckpointFilePreview(converted.Value.Content, PreviewName(file), FromCache: false));
        }
    }

    private static async Task<PreviewLockLease> AcquirePreviewLockAsync(Guid fileId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = PreviewLocks.GetOrAdd(fileId, static _ => new PreviewLockEntry());
            Interlocked.Increment(ref entry.ReferenceCount);
            if (PreviewLocks.TryGetValue(fileId, out var current) && ReferenceEquals(entry, current))
            {
                try
                {
                    await entry.Gate.WaitAsync(cancellationToken);
                    return new PreviewLockLease(fileId, entry);
                }
                catch
                {
                    ReleasePreviewLockReference(fileId, entry, releaseGate: false);
                    throw;
                }
            }

            ReleasePreviewLockReference(fileId, entry, releaseGate: false);
        }
    }

    private static void ReleasePreviewLockReference(Guid fileId, PreviewLockEntry entry, bool releaseGate)
    {
        if (releaseGate) entry.Gate.Release();
        if (Interlocked.Decrement(ref entry.ReferenceCount) == 0 &&
            PreviewLocks.TryRemove(new KeyValuePair<Guid, PreviewLockEntry>(fileId, entry)))
        {
            entry.Gate.Dispose();
        }
    }

    public async Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsStudent(role)) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "Only team students can delete submitted files.");
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);
        if (!access.Value.IsMember) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var availability = EnsureOpen(access.Value.Schedule, now);
        if (availability.IsFailure) return availability;
        var file = await context.SubmissionFiles.Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == fileId && item.Submission.TeamId == teamId && item.Submission.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
        if (file is null) return Result.Failure(ErrorCodes.CommonNotFoundError, "Submitted file was not found.");
        if (file.UploadedById != userId) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only delete files you uploaded.");
        file.IsDeleted = true; file.DeletedAt = now; file.DeletedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(file.CloudinaryPublicId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(file.PreviewPdfPublicId))
        {
            await storage.DeleteAsync(file.PreviewPdfPublicId, cancellationToken);
        }
        return Result.Success();
    }

    private async Task<Result<FileAccess>> ResolveFileAccessAsync(
        Guid teamId,
        int checkpointNumber,
        Guid fileId,
        Guid userId,
        string role,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<FileAccess>(access.Error);
        var query = context.SubmissionFiles.Include(item => item.Submission).AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        var file = await query.FirstOrDefaultAsync(item => item.Id == fileId &&
            item.Submission.TeamId == teamId && item.Submission.CheckpointId == access.Value.Checkpoint.Id,
            cancellationToken);
        return file is null
            ? Result.Failure<FileAccess>(ErrorCodes.CommonNotFoundError, "Submitted file was not found.")
            : Result.Success(new FileAccess(access.Value, file));
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
        var schedule = await context.ClassCheckpointSchedules.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ClassId == team.ClassId && item.CheckpointId == checkpoint.Id, cancellationToken);
        return Result.Success(new Access(checkpoint, project, schedule, isMember, team.TeamMembers.FirstOrDefault(member => member.ClassStudent.Student.UserId == userId)?.ClassStudent.Student.FullName ?? string.Empty));
    }

    private static bool HasExpectedSignature(Stream content, string extension)
    {
        var initialPosition = content.Position;
        try
        {
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                Span<byte> header = stackalloc byte[5];
                return content.Read(header) == header.Length && header.SequenceEqual("%PDF-"u8);
            }

            Span<byte> zipHeader = stackalloc byte[4];
            if (content.Read(zipHeader) != zipHeader.Length || zipHeader[0] != 0x50 || zipHeader[1] != 0x4B)
            {
                return false;
            }

            content.Position = initialPosition;
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? zip.GetEntry("word/document.xml") is not null
                : zip.GetEntry("ppt/presentation.xml") is not null;
        }
        catch (InvalidDataException) { return false; }
        finally { content.Position = initialPosition; }
    }

    private static string SafeOriginalName(string name) => Path.GetFileName(name).Trim()[..Math.Min(Path.GetFileName(name).Trim().Length, 256)];
    private static string PreviewName(SubmissionFile file) => $"{Path.GetFileNameWithoutExtension(file.OriginalName)}-preview-v{file.VersionNumber}.pdf";
    private static bool HasPdfSignature(byte[] content) => content.Length >= 5 && content.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
    private static Result<T> ConversionFailed<T>() => Result.Failure<T>(
        ErrorCodes.WorkspaceFilePreviewConversionFailed,
        "The stored PDF is invalid and cannot be previewed. You can still download the original file.");
    private static WorkspaceCheckpointFileResponse Map(SubmissionFile file, string userName) => new() { Id = file.Id, VersionNumber = file.VersionNumber, OriginalName = file.OriginalName, FileType = file.FileType.ToString(), FileSize = file.FileSize, UploadedAt = file.UploadedAt, UploadedBy = new WorkspaceCheckpointUserResponse { Id = file.UploadedById!.Value, Name = userName, Role = SystemRoles.Student } };
    private static bool IsSupportedRole(string role) => IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer) || IsRole(role, SystemRoles.Mentor) || IsRole(role, SystemRoles.Student);
    private static bool IsStudent(string role) => IsRole(role, SystemRoles.Student);
    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static Result EnsureOpen(ClassCheckpointSchedule? schedule, DateTime now)
    {
        if (schedule is not null && now >= schedule.StartDateUtc && now <= schedule.EndDateUtc) return Result.Success();
        var message = schedule is null
            ? "This checkpoint has not been scheduled for your class."
            : now < schedule.StartDateUtc
                ? $"This checkpoint opens at {schedule.StartDateUtc:O}."
                : $"This checkpoint closed at {schedule.EndDateUtc:O}.";
        return Result.Failure(ErrorCodes.WorkspaceCheckpointNotOpen, message);
    }
    private static Result<T> Denied<T>() => Result.Failure<T>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
    private static Result<T> Invalid<T>(string message) => Result.Failure<T>(ErrorCodes.WorkspaceValidationError, message);
    private sealed class PreviewLockEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int ReferenceCount;
    }

    private sealed class PreviewLockLease(Guid fileId, PreviewLockEntry entry) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                ReleasePreviewLockReference(fileId, entry, releaseGate: true);
            }
        }
    }

    private sealed record FileAccess(Access Workspace, SubmissionFile File);
    private sealed record Access(Checkpoint Checkpoint, Project Project, ClassCheckpointSchedule? Schedule, bool IsMember, string UserName);
}
