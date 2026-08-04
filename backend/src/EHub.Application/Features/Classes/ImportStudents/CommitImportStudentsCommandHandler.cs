using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
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
    private readonly IApplicationDbContext _context;
    private readonly IImportSessionStore _sessionStore;

    public CommitImportStudentsCommandHandler(
        IApplicationDbContext context,
        IImportSessionStore sessionStore)
    {
        _context = context;
        _sessionStore = sessionStore;
    }

    public async Task<Result<ImportStudentsCommitResponse>> HandleAsync(
        Guid classId,
        CommitImportStudentsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only Admin can commit student imports during the safety hardening period.");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid import sessionId is required.");
        }

        var acquireResult = _sessionStore.TryAcquireSession(request.SessionId, classId, currentUserId);
        if (acquireResult.Status != ImportSessionAcquireStatus.Acquired || acquireResult.Session == null)
        {
            return Failure(ErrorCodes.ClassImportSessionInvalid, GetSessionErrorMessage(acquireResult.Status));
        }

        var session = acquireResult.Session;

        try
        {
            var targetClass = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

            if (targetClass == null)
            {
                _sessionStore.ReleaseSession(request.SessionId);
                return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
            }

            if (targetClass.Status == ClassStatus.Archived)
            {
                _sessionStore.ReleaseSession(request.SessionId);
                return Failure(ErrorCodes.ClassArchived, "Cannot import students to an archived class.");
            }

            if (session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _sessionStore.ReleaseSession(request.SessionId);
                return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session has expired.");
            }

            if (session.ValidRows.Count == 0)
            {
                _sessionStore.ReleaseSession(request.SessionId);
                return Failure(ErrorCodes.ClassImportNoValidRows, "No valid student rows are available to commit.");
            }

            var studentCodes = session.ValidRows
                .Select(row => row.StudentCode.Trim().ToUpperInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var emails = session.ValidRows
                .Select(row => row.Email.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingProfiles = await _context.Students
                .Where(student =>
                    (student.NormalizedRollNumber != null && studentCodes.Contains(student.NormalizedRollNumber)) ||
                    (student.Email != null && emails.Contains(student.Email.ToLower())))
                .ToListAsync(cancellationToken);

            var existingStudentIds = existingProfiles.Select(student => student.Id).ToArray();
            List<ClassStudent> relevantEnrollments = existingStudentIds.Length == 0
                ? []
                : await _context.ClassStudents
                    .Include(enrollment => enrollment.Class)
                    .Where(enrollment =>
                        existingStudentIds.Contains(enrollment.StudentId) &&
                        (enrollment.ClassId == classId ||
                         (enrollment.EnrollmentStatus == EnrollmentStatus.Active &&
                          enrollment.Class.CourseId == targetClass.CourseId &&
                          enrollment.Class.SemesterId == targetClass.SemesterId &&
                          enrollment.Class.Status == ClassStatus.Active)))
                    .ToListAsync(cancellationToken);

            var profilesByCode = existingProfiles
                .Where(student => !string.IsNullOrWhiteSpace(student.NormalizedRollNumber))
                .ToDictionary(student => student.NormalizedRollNumber!, StringComparer.OrdinalIgnoreCase);
            var profilesByEmail = existingProfiles
                .Where(student => !string.IsNullOrWhiteSpace(student.Email))
                .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            var insertedCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;

            foreach (var row in session.ValidRows)
            {
                var studentCode = row.StudentCode.Trim().ToUpperInvariant();
                var email = row.Email.Trim().ToLowerInvariant();
                profilesByCode.TryGetValue(studentCode, out var profileByCode);
                profilesByEmail.TryGetValue(email, out var emailProfiles);
                var profileByEmail = emailProfiles?.Count == 1 ? emailProfiles[0] : null;

                if ((emailProfiles?.Count ?? 0) > 1 ||
                    (profileByCode != null && profileByEmail != null && profileByCode.Id != profileByEmail.Id) ||
                    (profileByEmail != null &&
                     !string.IsNullOrWhiteSpace(profileByEmail.NormalizedRollNumber) &&
                     !string.Equals(profileByEmail.NormalizedRollNumber, studentCode, StringComparison.OrdinalIgnoreCase)))
                {
                    skippedCount++;
                    continue;
                }

                var studentProfile = profileByCode ?? profileByEmail;
                if (studentProfile != null)
                {
                    var activeEnrollment = relevantEnrollments.FirstOrDefault(enrollment =>
                        enrollment.StudentId == studentProfile.Id &&
                        enrollment.EnrollmentStatus == EnrollmentStatus.Active &&
                        (enrollment.ClassId == classId ||
                         (enrollment.Class.CourseId == targetClass.CourseId &&
                          enrollment.Class.SemesterId == targetClass.SemesterId)));

                    if (activeEnrollment != null)
                    {
                        skippedCount++;
                        continue;
                    }
                }

                if (studentProfile == null)
                {
                    studentProfile = new Student
                    {
                        RollNumber = studentCode,
                        NormalizedRollNumber = studentCode,
                        FullName = row.FullName.Trim(),
                        Email = email,
                        MajorCode = row.MajorCode.Trim().ToUpperInvariant(),
                        Status = StudentStatus.Active,
                        CreatedBy = currentUserId
                    };
                    await _context.Students.AddAsync(studentProfile, cancellationToken);
                    profilesByCode[studentCode] = studentProfile;
                    profilesByEmail[email] = [studentProfile];
                    insertedCount++;
                }
                else
                {
                    studentProfile.RollNumber = studentCode;
                    studentProfile.NormalizedRollNumber = studentCode;
                    studentProfile.FullName = row.FullName.Trim();
                    studentProfile.Email = email;
                    studentProfile.MajorCode = row.MajorCode.Trim().ToUpperInvariant();
                    studentProfile.UpdatedBy = currentUserId;
                    updatedCount++;
                }

                var existingEnrollment = relevantEnrollments.FirstOrDefault(enrollment =>
                    enrollment.StudentId == studentProfile.Id && enrollment.ClassId == classId);

                if (existingEnrollment != null)
                {
                    existingEnrollment.EnrollmentStatus = EnrollmentStatus.Active;
                    existingEnrollment.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    await _context.ClassStudents.AddAsync(new ClassStudent
                    {
                        ClassId = classId,
                        StudentId = studentProfile.Id,
                        EnrollmentStatus = EnrollmentStatus.Active,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            // A single SaveChanges makes profile and enrollment mutations atomic.
            await _context.SaveChangesAsync(cancellationToken);
            _sessionStore.CompleteSession(request.SessionId);

            return Result.Success(new ImportStudentsCommitResponse
            {
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                SkippedCount = skippedCount,
                ErrorCount = skippedCount
            });
        }
        catch
        {
            // Allow a safe retry after unexpected database/infrastructure failures.
            _sessionStore.ReleaseSession(request.SessionId);
            throw;
        }
    }

    private static string GetSessionErrorMessage(ImportSessionAcquireStatus status) => status switch
    {
        ImportSessionAcquireStatus.UserMismatch => "Import session does not belong to the current user.",
        ImportSessionAcquireStatus.ClassMismatch => "Import session does not belong to the target class.",
        ImportSessionAcquireStatus.AlreadyProcessing => "Import session is already being committed.",
        _ => "Import session has expired, is invalid, or has already been committed."
    };

    private static Result<ImportStudentsCommitResponse> Failure(string code, string message) =>
        Result.Failure<ImportStudentsCommitResponse>(new Error(code, message));
}
