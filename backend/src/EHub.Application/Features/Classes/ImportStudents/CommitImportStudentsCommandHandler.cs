using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ImportStudents;

public sealed class CommitImportStudentsCommandHandler : ICommitImportStudentsCommandHandler
{
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public CommitImportStudentsCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ImportStudentsCommitResponse>> HandleAsync(
        Guid classId,
        CommitImportStudentsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to commit student imports.");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid import sessionId is required.");
        }

        var session = await _context.ClassImportSessions
            .FirstOrDefaultAsync(candidate => candidate.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session is invalid or has already been consumed.");
        }

        if (session.UserId != currentUserId || session.ClassId != classId)
        {
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session does not belong to the current user and class.");
        }

        var now = DateTime.UtcNow;
        if (session.ExpiresAtUtc <= now)
        {
            return Failure(ErrorCodes.ClassImportSessionExpired, "Import session has expired. Preview the file again.");
        }

        if (session.Status == ClassImportSessionStatus.Consumed)
        {
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session has already been consumed.");
        }

        if (session.Status == ClassImportSessionStatus.Processing &&
            session.ProcessingStartedAtUtc.HasValue &&
            session.ProcessingStartedAtUtc.Value.Add(ProcessingLease) > now)
        {
            return Failure(ErrorCodes.ClassImportSessionAlreadyProcessing, "Import session is already being committed.");
        }

        session.Status = ClassImportSessionStatus.Processing;
        session.ProcessingStartedAtUtc = now;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ClearChanges();
            return Failure(ErrorCodes.ClassImportSessionAlreadyProcessing, "Import session is already being committed.");
        }

        var targetClass = await _context.Classes
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(mutationError.Code, mutationError.Message);
        }

        if (isLecturer && targetClass.PrimaryLecturerId != currentUserId)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassAccessDenied, "You can only import students to your assigned class.");
        }

        ImportStudentRowPreviewDto[] rows;
        try
        {
            rows = JsonSerializer.Deserialize<ImportStudentRowPreviewDto[]>(session.ValidRowsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session payload is invalid. Preview the file again.");
        }

        if (rows.Length == 0)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassImportNoValidRows, "No valid student rows are available to commit.");
        }

        try
        {
            var response = await _unitOfWork.ExecuteInTransactionAsync(
                transactionCancellationToken => CommitRowsAsync(
                    targetClass,
                    session,
                    rows,
                    request.SynchronizeProfileMajors,
                    currentUserId,
                    transactionCancellationToken),
                cancellationToken);

            return Result.Success(response);
        }
        catch (DbUpdateException)
        {
            await ResetAfterFailureAsync(request.SessionId, cancellationToken);
            return Failure(
                ErrorCodes.ClassStudentEnrollmentConflict,
                "The import conflicted with another enrollment update. Preview the file again.");
        }
        catch
        {
            await ResetAfterFailureAsync(request.SessionId, cancellationToken);
            throw;
        }
    }

    private async Task<ImportStudentsCommitResponse> CommitRowsAsync(
        Class targetClass,
        ClassImportSession session,
        IReadOnlyCollection<ImportStudentRowPreviewDto> rows,
        bool synchronizeProfileMajors,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var codes = rows.Select(row => row.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = rows.Select(row => row.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var profiles = await _context.Students
            .Where(student =>
                (student.NormalizedRollNumber != null && codes.Contains(student.NormalizedRollNumber)) ||
                (student.RollNumber != null && codes.Contains(student.RollNumber)) ||
                (student.Email != null && emails.Contains(student.Email.ToLower())))
            .ToListAsync(cancellationToken);

        var profileIds = profiles.Select(student => student.Id).ToArray();
        var enrollments = profileIds.Length == 0
            ? []
            : await _context.ClassStudents
                .Include(enrollment => enrollment.Class)
                .Where(enrollment =>
                    profileIds.Contains(enrollment.StudentId) &&
                    (enrollment.ClassId == targetClass.Id ||
                     (enrollment.CountsTowardCourseSemesterLimit &&
                      enrollment.SemesterId == targetClass.SemesterId &&
                      enrollment.CourseId == targetClass.CourseId)))
                .ToListAsync(cancellationToken);

        var profilesByCode = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.NormalizedRollNumber ?? student.RollNumber))
            .GroupBy(student => student.NormalizedRollNumber ?? student.RollNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var profilesByEmail = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.Email))
            .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var insertedCount = 0;
        var updatedCount = 0;
        var synchronizedMajorCount = 0;
        var errors = new List<ImportStudentCommitErrorDto>();

        foreach (var row in rows)
        {
            profilesByCode.TryGetValue(row.StudentCode, out var codeProfiles);
            var profileByCode = codeProfiles?.Count == 1 ? codeProfiles[0] : null;
            profilesByEmail.TryGetValue(row.Email, out var emailProfiles);
            var profileByEmail = emailProfiles?.Count == 1 ? emailProfiles[0] : null;

            if ((codeProfiles?.Count ?? 0) > 1 ||
                (emailProfiles?.Count ?? 0) > 1 ||
                (profileByCode != null && profileByEmail != null && profileByCode.Id != profileByEmail.Id) ||
                (profileByCode != null && !string.Equals(profileByCode.Email, row.Email, StringComparison.OrdinalIgnoreCase)) ||
                (profileByEmail != null &&
                 !string.Equals(profileByEmail.NormalizedRollNumber ?? profileByEmail.RollNumber, row.StudentCode, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(RowError(row, ErrorCodes.ClassStudentIdentityConflict, "Student code and email no longer identify one unique student profile."));
                continue;
            }

            var profile = profileByCode ?? profileByEmail;
            var currentEnrollment = profile == null
                ? null
                : enrollments.FirstOrDefault(enrollment =>
                    enrollment.StudentId == profile.Id && enrollment.ClassId == targetClass.Id);
            if (currentEnrollment != null)
            {
                errors.Add(RowError(
                    row,
                    currentEnrollment.EnrollmentStatus == EnrollmentStatus.Dropped
                        ? ErrorCodes.ClassStudentReEnrollmentRequired
                        : ErrorCodes.ClassStudentAlreadyEnrolled,
                    currentEnrollment.EnrollmentStatus == EnrollmentStatus.Dropped
                        ? "Student has a dropped enrollment. Use the explicit re-enroll action."
                        : "Student already has an enrollment in this class."));
                continue;
            }

            var conflict = profile == null
                ? null
                : enrollments.FirstOrDefault(enrollment =>
                    enrollment.StudentId == profile.Id && enrollment.CountsTowardCourseSemesterLimit);
            if (conflict != null)
            {
                errors.Add(RowError(
                    row,
                    ErrorCodes.ClassStudentEnrollmentConflict,
                    $"Student is already enrolled in class '{conflict.Class.ClassCode}' for the same course and semester."));
                continue;
            }

            if (profile == null)
            {
                profile = new Student
                {
                    RollNumber = row.StudentCode,
                    NormalizedRollNumber = row.StudentCode,
                    FullName = row.FullName,
                    Email = row.Email,
                    MajorCode = MajorCodes.IsValid(row.MajorCode) ? row.MajorCode : null,
                    Status = StudentStatus.Active,
                    CreatedBy = currentUserId
                };
                _context.Students.Add(profile);
                profilesByCode[row.StudentCode] = [profile];
                profilesByEmail[row.Email] = [profile];
            }
            else if (synchronizeProfileMajors &&
                     profile.UserId.HasValue &&
                     MajorCodes.IsValid(row.MajorCode))
            {
                var importedMajor = row.MajorCode.Trim().ToUpperInvariant();
                var registeredMajor = profile.MajorCode?.Trim().ToUpperInvariant();
                if (!string.Equals(importedMajor, registeredMajor, StringComparison.OrdinalIgnoreCase))
                {
                    profile.MajorCode = importedMajor;
                    profile.UpdatedAt = DateTime.UtcNow;
                    profile.UpdatedBy = currentUserId;
                    synchronizedMajorCount++;
                }
            }

            currentEnrollment = new ClassStudent
            {
                ClassId = targetClass.Id,
                StudentId = profile.Id,
                SemesterId = targetClass.SemesterId,
                CourseId = targetClass.CourseId,
                EnrollmentStatus = EnrollmentStatus.Active,
                CountsTowardCourseSemesterLimit = true,
                MajorCodeAtEnrollment = row.MajorCode,
                MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ClassStudents.Add(currentEnrollment);
            enrollments.Add(currentEnrollment);
            insertedCount++;
        }

        session.Status = ClassImportSessionStatus.Consumed;
        session.ConsumedAtUtc = DateTime.UtcNow;
        session.ProcessingStartedAtUtc = null;

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = targetClass.Id,
            Action = "STUDENT_IMPORT_COMMITTED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                SessionId = session.Id,
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                SynchronizedMajorCount = synchronizedMajorCount,
                SynchronizeProfileMajors = synchronizeProfileMajors,
                ErrorCount = errors.Count
            }, JsonOptions)
        });
        ClassOutbox.Enqueue(_context, "Class.StudentRosterImported.v1", targetClass.Id, new
        {
            SessionId = session.Id,
            InsertedCount = insertedCount,
            ErrorCount = errors.Count
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new ImportStudentsCommitResponse
        {
            InsertedCount = insertedCount,
            UpdatedCount = updatedCount,
            SynchronizedMajorCount = synchronizedMajorCount,
            SkippedCount = errors.Count,
            ErrorCount = errors.Count,
            Errors = errors
        };
    }

    private async Task ReleaseAsync(ClassImportSession session, CancellationToken cancellationToken)
    {
        session.Status = ClassImportSessionStatus.Available;
        session.ProcessingStartedAtUtc = null;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetAfterFailureAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            _context.ClearChanges();
            var session = await _context.ClassImportSessions
                .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken);
            if (session?.Status == ClassImportSessionStatus.Processing)
            {
                session.Status = ClassImportSessionStatus.Available;
                session.ProcessingStartedAtUtc = null;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // The lease permits recovery if infrastructure is unavailable here.
        }
    }

    private static ImportStudentCommitErrorDto RowError(
        ImportStudentRowPreviewDto row,
        string errorCode,
        string errorMessage) => new()
    {
        RowNumber = row.RowNumber,
        StudentCode = row.StudentCode,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    private static Result<ImportStudentsCommitResponse> Failure(string code, string message) =>
        Result.Failure<ImportStudentsCommitResponse>(new Error(code, message));
}
