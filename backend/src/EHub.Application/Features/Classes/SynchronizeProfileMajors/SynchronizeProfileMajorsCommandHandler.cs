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

namespace EHub.Application.Features.Classes.SynchronizeProfileMajors;

public sealed class SynchronizeProfileMajorsCommandHandler : ISynchronizeProfileMajorsCommandHandler
{
    private readonly IApplicationDbContext _context;

    public SynchronizeProfileMajorsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SynchronizeProfileMajorsResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!ClassAuthorizationRules.IsStaff(currentUserRole))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can synchronize registered majors.");
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (!ClassAuthorizationRules.CanManageClass(targetClass.PrimaryLecturerId, currentUserId, currentUserRole))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only synchronize majors for classes assigned to you.");
        }

        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null)
        {
            return Result.Failure<SynchronizeProfileMajorsResponse>(mutationError);
        }

        var enrollments = await _context.ClassStudents
            .Include(item => item.Student)
            .Where(item => item.ClassId == classId && item.EnrollmentStatus == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
        var rosterEmails = enrollments
            .Where(item => !string.IsNullOrWhiteSpace(item.Student.Email))
            .Select(item => item.Student.Email!.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        var linkedProfilesByEmail = (await _context.Students
                .Where(student => student.UserId.HasValue &&
                    student.Email != null &&
                    rosterEmails.Contains(student.Email.ToLower()))
                .ToListAsync(cancellationToken))
            .Where(student => !string.IsNullOrWhiteSpace(student.Email))
            .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

        var mismatchCount = 0;
        var synchronizedCount = 0;
        var now = DateTime.UtcNow;
        foreach (var enrollment in enrollments)
        {
            if (!MajorCodes.IsValid(enrollment.MajorCodeAtEnrollment)) continue;

            var profile = enrollment.Student.UserId.HasValue
                ? enrollment.Student
                : !string.IsNullOrWhiteSpace(enrollment.Student.Email) &&
                  linkedProfilesByEmail.TryGetValue(enrollment.Student.Email, out var linkedProfile)
                    ? linkedProfile
                    : null;
            if (profile == null) continue; // The student has not registered yet.

            var officialMajor = enrollment.MajorCodeAtEnrollment.Trim().ToUpperInvariant();
            if (string.Equals(profile.MajorCode?.Trim(), officialMajor, StringComparison.OrdinalIgnoreCase)) continue;

            mismatchCount++;
            profile.MajorCode = officialMajor;
            profile.UpdatedAt = now;
            profile.UpdatedBy = currentUserId;
            synchronizedCount++;
        }

        if (synchronizedCount > 0)
        {
            _context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = classId,
                Action = "STUDENT_PROFILE_MAJORS_SYNCHRONIZED",
                PerformedByUserId = currentUserId,
                OccurredAtUtc = now,
                DetailsJson = JsonSerializer.Serialize(new { MismatchCount = mismatchCount, SynchronizedCount = synchronizedCount })
            });
            ClassOutbox.Enqueue(_context, "Class.StudentProfileMajorsSynchronized.v1", classId, new
            {
                MismatchCount = mismatchCount,
                SynchronizedCount = synchronizedCount
            }, now);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new SynchronizeProfileMajorsResponse
        {
            MismatchCount = mismatchCount,
            SynchronizedCount = synchronizedCount
        });
    }

    private static Result<SynchronizeProfileMajorsResponse> Failure(string code, string message) =>
        Result.Failure<SynchronizeProfileMajorsResponse>(new Error(code, message));
}
