using System.Security.Cryptography;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Users;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.Users.ImportLecturers;

public sealed class LecturerImportHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : ILecturerImportHandler
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<LecturerImportPreviewResponse>> PreviewAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out var adminUserId))
        {
            return Failure<LecturerImportPreviewResponse>(
                ErrorCodes.CommonUnauthorizedError,
                "An authenticated administrator is required.");
        }

        if (file is null || file.Length == 0)
        {
            return Failure<LecturerImportPreviewResponse>(
                ErrorCodes.LecturerImportFileInvalid,
                "Select a non-empty Excel file.");
        }

        if (file.Length > MaximumFileSize)
        {
            return Failure<LecturerImportPreviewResponse>(
                ErrorCodes.LecturerImportFileInvalid,
                "The lecturer import file may not exceed 5 MB.");
        }

        var securityResult = ExcelWorkbookSecurity.Validate(file);
        if (securityResult.IsFailure)
        {
            return Failure<LecturerImportPreviewResponse>(
                ErrorCodes.LecturerImportFileInvalid,
                securityResult.Error.Message);
        }

        var parseResult = LecturerImportWorkbookParser.Parse(file);
        if (parseResult.IsFailure)
        {
            return Result.Failure<LecturerImportPreviewResponse>(parseResult.Error);
        }

        var rows = parseResult.Value;
        await ApplyDatabaseValidationAsync(rows, cancellationToken);

        var errorCount = rows.Count(row => !row.IsValid);
        var readyCount = rows.Count(row => row.Status == "Ready");
        var willActivateCount = rows.Count(row => row.Status == "WillActivate");
        var existingCount = rows.Count(row => row.Status == "AlreadyExists");
        var canCommit = errorCount == 0 && readyCount + willActivateCount > 0;
        var sessionId = Guid.Empty;

        if (canCommit)
        {
            sessionId = Guid.NewGuid();
            var now = dateTimeProvider.UtcNow;
            context.LecturerImportSessions.Add(new LecturerImportSession
            {
                Id = sessionId,
                AdminUserId = adminUserId,
                RowsJson = JsonSerializer.Serialize(rows, JsonOptions),
                Status = LecturerImportSessionStatus.Available,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(SessionLifetime)
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new LecturerImportPreviewResponse
        {
            SessionId = sessionId,
            TotalRows = rows.Count,
            ReadyCount = readyCount,
            WillActivateCount = willActivateCount,
            ExistingCount = existingCount,
            ErrorCount = errorCount,
            CanCommit = canCommit,
            Rows = rows.Select(ToPreviewRow).ToArray()
        });
    }

    public async Task<Result<LecturerImportCommitResponse>> CommitAsync(
        CommitLecturerImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out var adminUserId))
        {
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.CommonUnauthorizedError,
                "An authenticated administrator is required.");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportSessionInvalid,
                "A valid lecturer import session is required.");
        }

        var session = await context.LecturerImportSessions
            .FirstOrDefaultAsync(candidate => candidate.Id == request.SessionId, cancellationToken);
        if (session is null || session.AdminUserId != adminUserId)
        {
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportSessionInvalid,
                "The lecturer import session is invalid or belongs to another administrator.");
        }

        var now = dateTimeProvider.UtcNow;
        if (session.ExpiresAtUtc <= now)
        {
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportSessionExpired,
                "The lecturer import session has expired. Preview the file again.");
        }

        if (session.Status == LecturerImportSessionStatus.Consumed)
        {
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportSessionInvalid,
                "The lecturer import session has already been used.");
        }

        if (session.Status == LecturerImportSessionStatus.Processing &&
            session.ProcessingStartedAtUtc.HasValue &&
            session.ProcessingStartedAtUtc.Value.Add(ProcessingLease) > now)
        {
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportSessionAlreadyProcessing,
                "The lecturer import session is already being processed.");
        }

        session.Status = LecturerImportSessionStatus.Processing;
        session.ProcessingStartedAtUtc = now;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ClearChanges();
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportSessionAlreadyProcessing,
                "The lecturer import session is already being processed.");
        }

        LecturerImportCandidate[] rows;
        try
        {
            rows = JsonSerializer.Deserialize<LecturerImportCandidate[]>(session.RowsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportSessionInvalid,
                "The lecturer import session is damaged. Preview the file again.");
        }

        if (rows.Length == 0 || rows.Any(row => !row.IsValid))
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportNoActionableRows,
                "The lecturer import contains no safe set of rows to commit.");
        }

        try
        {
            var response = await unitOfWork.ExecuteInTransactionAsync(
                transactionCancellationToken => CommitRowsAsync(
                    session,
                    rows,
                    adminUserId,
                    transactionCancellationToken),
                cancellationToken);

            return Result.Success(response);
        }
        catch (DbUpdateException)
        {
            await ResetAfterFailureAsync(request.SessionId, cancellationToken);
            return Failure<LecturerImportCommitResponse>(
                ErrorCodes.LecturerImportConflict,
                "Lecturer accounts changed while the import was being committed. Preview the file again.");
        }
        catch
        {
            await ResetAfterFailureAsync(request.SessionId, cancellationToken);
            throw;
        }
    }

    private async Task ApplyDatabaseValidationAsync(
        IReadOnlyCollection<LecturerImportCandidate> rows,
        CancellationToken cancellationToken)
    {
        var emails = rows
            .Where(row => row.IsValid)
            .Select(row => row.GoogleEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (emails.Length == 0) return;

        var users = await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(user => emails.Contains(user.NormalizedEmail))
            .ToListAsync(cancellationToken);
        var byEmail = users.ToDictionary(user => user.NormalizedEmail, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Where(row => row.IsValid))
        {
            if (!byEmail.TryGetValue(row.GoogleEmail, out var user)) continue;

            var isLecturer = user.UserRoles.Any(userRole =>
                userRole.Role.Name == SystemRoles.Lecturer);
            if (user.IsDeleted)
            {
                MarkConflict(row, "A deleted account already uses this login email.");
            }
            else if (!isLecturer)
            {
                MarkConflict(row, "This login email already belongs to a non-Lecturer account.");
            }
            else if (user.Status == UserStatus.Active)
            {
                row.Status = "AlreadyExists";
                row.Message = "An active Lecturer account already exists; this row will be skipped.";
            }
            else if (user.Status == UserStatus.PendingApproval)
            {
                row.Status = "WillActivate";
                row.Message = "The existing pending Lecturer account will be activated.";
            }
            else
            {
                MarkConflict(row, $"The existing Lecturer account is {user.Status} and cannot be changed by import.");
            }
        }
    }

    private async Task<LecturerImportCommitResponse> CommitRowsAsync(
        LecturerImportSession session,
        IReadOnlyCollection<LecturerImportCandidate> rows,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .FirstOrDefaultAsync(candidate => candidate.Name == SystemRoles.Lecturer, cancellationToken)
            ?? throw new InvalidOperationException("The Lecturer role has not been seeded.");
        var emails = rows.Select(row => row.GoogleEmail).Distinct().ToArray();
        var users = await context.Users
            .IgnoreQueryFilters()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(user => emails.Contains(user.NormalizedEmail))
            .ToListAsync(cancellationToken);
        var usersByEmail = users.ToDictionary(user => user.NormalizedEmail, StringComparer.OrdinalIgnoreCase);
        var pendingRegistrations = await context.PendingRegistrations
            .Where(candidate => emails.Contains(candidate.NormalizedEmail) &&
                                candidate.Status == PendingRegistrationStatus.Pending)
            .ToListAsync(cancellationToken);
        var pendingByEmail = pendingRegistrations
            .ToDictionary(candidate => candidate.NormalizedEmail, StringComparer.OrdinalIgnoreCase);

        var createdCount = 0;
        var activatedCount = 0;
        var skippedCount = 0;
        var errors = new List<LecturerImportCommitError>();
        var now = dateTimeProvider.UtcNow;

        foreach (var row in rows)
        {
            if (usersByEmail.TryGetValue(row.GoogleEmail, out var existing))
            {
                var isLecturer = existing.UserRoles.Any(userRole =>
                    userRole.Role.Name == SystemRoles.Lecturer);
                if (isLecturer && !existing.IsDeleted && existing.Status == UserStatus.Active)
                {
                    skippedCount++;
                    continue;
                }

                if (isLecturer && !existing.IsDeleted && existing.Status == UserStatus.PendingApproval)
                {
                    existing.Status = UserStatus.Active;
                    existing.UpdatedBy = adminUserId;
                    activatedCount++;
                    CancelPendingRegistration(row.GoogleEmail, existing.Id, pendingByEmail, now, adminUserId);
                    continue;
                }

                errors.Add(RowError(row, "ACCOUNT_CONFLICT", "The login email is no longer safe to import."));
                continue;
            }

            var generatedPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var user = new User
            {
                FullName = row.FullName,
                Email = row.GoogleEmail,
                NormalizedEmail = row.GoogleEmail,
                PasswordHash = passwordHasher.Hash(generatedPassword),
                Status = UserStatus.Active,
                IsEmailVerified = false,
                CreatedBy = adminUserId
            };
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAt = now,
                AssignedBy = adminUserId
            });
            usersByEmail[row.GoogleEmail] = user;
            createdCount++;
            CancelPendingRegistration(row.GoogleEmail, user.Id, pendingByEmail, now, adminUserId);
        }

        session.Status = LecturerImportSessionStatus.Consumed;
        session.ConsumedAtUtc = now;
        session.ProcessingStartedAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);

        return new LecturerImportCommitResponse
        {
            CreatedCount = createdCount,
            ActivatedCount = activatedCount,
            SkippedCount = skippedCount,
            ErrorCount = errors.Count,
            Errors = errors
        };
    }

    private static void CancelPendingRegistration(
        string email,
        Guid completedUserId,
        IReadOnlyDictionary<string, PendingRegistration> pendingByEmail,
        DateTime now,
        Guid adminUserId)
    {
        if (!pendingByEmail.TryGetValue(email, out var pending)) return;

        pending.Status = PendingRegistrationStatus.Cancelled;
        pending.CompletedUserId = completedUserId;
        pending.CompletedAtUtc = now;
        pending.UpdatedBy = adminUserId;
    }

    private async Task ReleaseAsync(
        LecturerImportSession session,
        CancellationToken cancellationToken)
    {
        session.Status = LecturerImportSessionStatus.Available;
        session.ProcessingStartedAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetAfterFailureAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            context.ClearChanges();
            var session = await context.LecturerImportSessions
                .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken);
            if (session?.Status != LecturerImportSessionStatus.Processing) return;

            session.Status = LecturerImportSessionStatus.Available;
            session.ProcessingStartedAtUtc = null;
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The processing lease allows recovery if the database is unavailable here.
        }
    }

    private bool TryGetAdminId(out Guid adminUserId)
    {
        adminUserId = currentUser.UserId ?? Guid.Empty;
        return currentUser.IsAuthenticated && adminUserId != Guid.Empty &&
               currentUser.Roles.Any(role => role.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase));
    }

    private static LecturerImportRowPreview ToPreviewRow(LecturerImportCandidate row) => new()
    {
        RowNumber = row.RowNumber,
        FullName = row.FullName,
        Position = row.Position,
        ContactEmail = row.ContactEmail,
        GoogleEmail = row.GoogleEmail,
        Status = row.Status,
        IsValid = row.IsValid,
        Message = row.Message
    };

    private static void MarkConflict(LecturerImportCandidate row, string message)
    {
        row.IsValid = false;
        row.Status = "Conflict";
        row.Message = message;
    }

    private static LecturerImportCommitError RowError(
        LecturerImportCandidate row,
        string errorCode,
        string errorMessage) => new()
    {
        RowNumber = row.RowNumber,
        GoogleEmail = row.GoogleEmail,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(new Error(code, message));
}
