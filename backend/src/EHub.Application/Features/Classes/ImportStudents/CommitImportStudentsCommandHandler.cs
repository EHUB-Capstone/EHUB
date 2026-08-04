using System;
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
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ImportStudentsCommitResponse>(
                new Error("Classes.AccessDenied", "You do not have permission to commit student import."));
        }

        var sessionData = _sessionStore.GetAndConsumeSession(request.SessionId);
        if (sessionData == null)
        {
            return Result.Failure<ImportStudentsCommitResponse>(
                new Error("Classes.ImportSessionExpiredOrProcessed", "Import session has expired, is invalid, or has already been committed."));
        }

        var (targetClassId, sessionUserId, validRows) = sessionData.Value;

        if (targetClassId != classId)
        {
            return Result.Failure<ImportStudentsCommitResponse>(
                new Error("Classes.ImportSessionMismatch", "Import session does not match the target class."));
        }

        if (validRows == null || validRows.Count == 0)
        {
            return Result.Failure<ImportStudentsCommitResponse>(
                new Error("Classes.NoValidRowsToImport", "No valid student rows to commit from this session."));
        }

        int insertedCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;

        foreach (var row in validRows)
        {
            var studentCode = row.StudentCode.Trim().ToUpperInvariant();
            var email = row.Email.Trim().ToLowerInvariant();

            var studentProfile = await _context.Students
                .FirstOrDefaultAsync(s => s.NormalizedRollNumber == studentCode || (s.Email != null && s.Email.ToLower() == email), cancellationToken);

            if (studentProfile == null)
            {
                studentProfile = new Student
                {
                    RollNumber = studentCode,
                    NormalizedRollNumber = studentCode,
                    FullName = row.FullName.Trim(),
                    Email = email,
                    MajorCode = row.MajorCode,
                    Status = StudentStatus.Active
                };
                await _context.Students.AddAsync(studentProfile, cancellationToken);
                insertedCount++;
            }
            else
            {
                studentProfile.FullName = row.FullName.Trim();
                studentProfile.Email = email;
                studentProfile.MajorCode = row.MajorCode;
                updatedCount++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var existingEnrollment = await _context.ClassStudents
                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentProfile.Id, cancellationToken);

            if (existingEnrollment == null)
            {
                var newEnrollment = new ClassStudent
                {
                    ClassId = classId,
                    StudentId = studentProfile.Id,
                    EnrollmentStatus = EnrollmentStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.ClassStudents.AddAsync(newEnrollment, cancellationToken);
            }
            else if (existingEnrollment.EnrollmentStatus != EnrollmentStatus.Active)
            {
                existingEnrollment.EnrollmentStatus = EnrollmentStatus.Active;
                existingEnrollment.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                skippedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var response = new ImportStudentsCommitResponse
        {
            InsertedCount = insertedCount,
            UpdatedCount = updatedCount,
            SkippedCount = skippedCount,
            ErrorCount = 0
        };

        return Result.Success(response);
    }
}
